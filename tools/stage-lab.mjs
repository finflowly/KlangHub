import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import { readFile, mkdir, writeFile, rm } from 'node:fs/promises'
import { existsSync } from 'node:fs'
import { join, extname, resolve } from 'node:path'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'

const here = fileURLToPath(new URL('.', import.meta.url))
const receiverDir = resolve(here, '..', 'receiver')

const query = process.argv[2] ?? 'demo=lab'
const outDir = process.argv[3] ?? join(tmpdir(), 'klanghub-stage-lab')
const runFor = Number(process.argv[4] ?? 16000)

const types = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.json': 'application/json',
}

function browser() {
  const candidates = [
    join(process.env['PROGRAMFILES'] ?? '', 'Google/Chrome/Application/chrome.exe'),
    join(process.env['PROGRAMFILES(X86)'] ?? '', 'Google/Chrome/Application/chrome.exe'),
    join(process.env['LOCALAPPDATA'] ?? '', 'Google/Chrome/Application/chrome.exe'),
    join(process.env['PROGRAMFILES(X86)'] ?? '', 'Microsoft/Edge/Application/msedge.exe'),
    join(process.env['PROGRAMFILES'] ?? '', 'Microsoft/Edge/Application/msedge.exe'),
    '/usr/bin/google-chrome',
    '/usr/bin/chromium',
  ]
  const found = candidates.find(p => p && existsSync(p))
  if (!found) throw new Error('No Chrome or Edge found to run the stage in.')
  return found
}

async function serveReceiver() {
  const server = createServer(async (request, response) => {
    const path = (request.url ?? '/').split('?')[0]
    const file = join(receiverDir, path === '/' ? 'index.html' : path.replace(/^\/+/, ''))

    if (!file.startsWith(receiverDir)) {
      response.writeHead(403).end()
      return
    }

    try {
      const body = await readFile(file)
      response.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' })
      response.end(body)
    } catch {
      response.writeHead(404).end()
    }
  })

  await new Promise(done => server.listen(0, '127.0.0.1', done))
  return { server, port: server.address().port }
}

async function debuggerUrl(port) {
  for (let attempt = 0; attempt < 100; attempt++) {
    try {
      const answer = await fetch(`http://127.0.0.1:${port}/json/list`)
      const targets = await answer.json()
      const page = targets.find(t => t.type === 'page' && t.webSocketDebuggerUrl)
      if (page) return page.webSocketDebuggerUrl
    } catch {
      // the browser has not opened its port yet
    }
    await new Promise(done => setTimeout(done, 100))
  }
  throw new Error('The browser never opened a page to attach to.')
}

function protocol(socket) {
  let next = 1
  const waiting = new Map()
  const listeners = []

  socket.addEventListener('message', event => {
    const message = JSON.parse(event.data)
    if (message.id && waiting.has(message.id)) {
      const { resolve: done, reject } = waiting.get(message.id)
      waiting.delete(message.id)
      message.error ? reject(new Error(message.error.message)) : done(message.result)
      return
    }
    for (const listener of listeners) listener(message)
  })

  return {
    send(method, params) {
      const id = next++
      socket.send(JSON.stringify({ id, method, params: params ?? {} }))
      return new Promise((done, reject) => waiting.set(id, { resolve: done, reject }))
    },
    on(listener) {
      listeners.push(listener)
    },
  }
}

const READ_STAGE = `(function () {
  var art = document.getElementById('art');
  var cover = document.getElementById('cover');
  return JSON.stringify({
    title: document.getElementById('title').textContent,
    artist: document.getElementById('artist').textContent,
    album: document.getElementById('album').textContent,
    zone: document.getElementById('zone').textContent,
    rings: art.classList.contains('empty'),
    cover: cover.getAttribute('src') ? (cover.getAttribute('src').slice(0, 60)) : '',
    body: document.body.className
  });
})()`

function describe(state) {
  const art = state.rings ? 'rings' : `cover ${state.cover}`
  return `title="${state.title}" artist="${state.artist}" album="${state.album}" ${art} body="${state.body}"`
}

const EXPECTED = [
  { step: 1, title: 'Teardrop', artist: 'Massive Attack', cover: true, cut: true },
  { step: 2, title: 'Broken Nights', artist: 'Patiotic', cover: false, cut: true },
  { step: 3, title: 'Get Lucky', artist: 'Daft Punk', cover: false, cut: false },
  { step: 4, title: 'Get Lucky', artist: 'Daft Punk', cover: true, cut: false },
  { step: 5, title: 'Blinding Lights', artist: 'The Weeknd', cover: false, cut: true },
]

function judge(seen) {
  const faults = []

  for (const want of EXPECTED) {
    const step = seen.get(want.step)

    if (!step || !step.last) {
      faults.push(`step ${want.step}: the stage never settled`)
      continue
    }

    const last = step.last
    if (last.title !== want.title) faults.push(`step ${want.step}: title is "${last.title}", not "${want.title}"`)
    if (last.artist !== want.artist) faults.push(`step ${want.step}: artist is "${last.artist}", not "${want.artist}"`)

    const hasCover = !last.rings
    if (hasCover !== want.cover) {
      faults.push(want.cover
        ? `step ${want.step}: the cover never appeared`
        : `step ${want.step}: the cover of the track before it is still on the stage (${last.cover})`)
    }

    if (!want.cut && step.cut) faults.push(`step ${want.step}: the words paid for a cut they should not have`)
  }

  return faults
}

const { server, port } = await serveReceiver()
await mkdir(outDir, { recursive: true })

const profile = join(tmpdir(), `klanghub-stage-lab-profile-${port}`)
const debugPort = port + 1
const chrome = spawn(browser(), [
  '--headless=new',
  `--remote-debugging-port=${debugPort}`,
  `--user-data-dir=${profile}`,
  '--window-size=1280,720',
  '--force-device-scale-factor=1',
  '--no-first-run',
  '--no-default-browser-check',
  '--disable-extensions',
  '--hide-scrollbars',
  'about:blank',
], { stdio: 'ignore' })

const log = []
function note(line) {
  log.push(line)
  console.log(line)
}

let shots = 0

try {
  const socket = new WebSocket(await debuggerUrl(debugPort))
  await new Promise((done, failed) => {
    socket.addEventListener('open', done, { once: true })
    socket.addEventListener('error', failed, { once: true })
  })

  const cdp = protocol(socket)
  const shoot = async name => {
    const shot = await cdp.send('Page.captureScreenshot', { format: 'png' })
    const file = join(outDir, `${String(++shots).padStart(2, '0')}-${name}.png`)
    await writeFile(file, Buffer.from(shot.data, 'base64'))
    note(`      screenshot -> ${file}`)
  }

  const steps = []
  const seen = new Map()
  let at = 0

  cdp.on(message => {
    if (message.method !== 'Runtime.consoleAPICalled') return

    const said = (message.params.args ?? []).map(a => a.value ?? a.description ?? '').join(' ')
    const step = said.match(/^lab: (\d+)/)

    if (step) {
      at = Number(step[1])
      seen.set(at, { last: null, cut: false })
      steps.push(said)
    }

    if (said.startsWith('lab:') || said.startsWith('stage:')) note(`  console: ${said}`)
  })

  await cdp.send('Page.enable')
  await cdp.send('Runtime.enable')
  await cdp.send('Page.navigate', { url: `http://127.0.0.1:${port}/index.html?${query}` })

  note(`# stage lab: ?${query}`)

  let last = ''
  let handled = 0
  const pending = []
  const started = Date.now()

  while (Date.now() - started < runFor) {
    await new Promise(done => setTimeout(done, 150))

    const read = await cdp.send('Runtime.evaluate', { expression: READ_STAGE, returnByValue: true })
    if (read.result?.value === undefined) continue

    const state = JSON.parse(read.result.value)
    const line = describe(state)

    const step = seen.get(at)
    if (step) {
      step.last = state
      if (state.body.indexOf('changing') >= 0) step.cut = true
    }

    if (line !== last) {
      last = line
      note(`+${String(Date.now() - started).padStart(5)}ms  [${at}] ${line}`)
    }

    while (handled < steps.length) {
      const said = steps[handled++]
      pending.push({ name: said.replace(/[^a-z0-9]+/gi, '-').replace(/^-|-$/g, '').toLowerCase().slice(0, 40),
                     due: Date.now() + 1500 })
    }

    while (pending.length && pending[0].due <= Date.now()) await shoot(pending.shift().name)
  }

  const faults = judge(seen)

  note('')
  if (faults.length === 0) {
    note(`# the stage did what it should, in all ${EXPECTED.length} steps`)
  } else {
    note(`# ${faults.length} fault(s) on the stage:`)
    for (const fault of faults) note(`#   ${fault}`)
    process.exitCode = 1
  }

  await writeFile(join(outDir, 'stage-lab.log'), log.join('\n') + '\n')
  note(`# log -> ${join(outDir, 'stage-lab.log')}`)
} finally {
  chrome.kill()
  server.close()
  await rm(profile, { recursive: true, force: true }).catch(() => {})
}
