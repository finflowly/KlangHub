; KlangHub setup — Inno Setup script
;
; Builds a single installer that carries every European language it can and picks the right one by itself:
; Inno matches the user's Windows UI language against the [Languages] list below and only asks when it
; cannot (ShowLanguageDialog=auto). Each language is NAMED after its ISO code, so {language} is exactly the
; code KlangHub expects — the app is launched with --lang={language} at the end of setup and therefore comes
; up in the same language the installation just spoke.
;
; Build:  "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\KlangHub.iss
; Input:  publish\KlangHub-1.0-win-x64  (dotnet publish -c Release -r win-x64 --self-contained true)

#define AppName        "KlangHub"
#define AppVersion     "1.0"
#define AppPublisher   "Neo & Trinity"
#define AppExe         "KlangHub.exe"
#define SourceDir      "..\publish\KlangHub-1.0-win-x64"

[Setup]
AppId={{7C2F4E11-9A63-4D8B-B0F2-3E5A1C6D8B44}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion=1.0.0.0
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} {#AppVersion}
OutputDir=..\dist
OutputBaseFilename=KlangHub-{#AppVersion}-Setup
SetupIconFile=..\Source\KlangHub\KlangHub.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
DisableProgramGroupPage=yes
DisableWelcomePage=no
; The brand artwork - the same picture the app wears and the Chromecast puts on the TV - in the sizes Inno
; picks from per display scaling.
WizardImageFile=wizard-large.bmp,wizard-large-125.bmp,wizard-large-150.bmp,wizard-large-200.bmp
WizardSmallImageFile=wizard-small.bmp,wizard-small-125.bmp,wizard-small-150.bmp,wizard-small-200.bmp
WizardImageStretch=yes
; Per-user by default: no UAC prompt, no admin account needed. Anyone who wants it machine-wide can still
; choose that in the dialog.
; Per-user install: no UAC prompt, no admin account, and no grey "installation mode" system dialog in
; front of the branded wizard.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Setup itself follows Windows without asking - its own language dialog is a bare system window that would
; break the look before the wizard even appears. The question is asked instead on the wizard's own first
; page (see [Code]), in the app's palette, and that answer is what KlangHub is started with.
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage
; A running KlangHub is closed and started again afterwards, without asking. The alternative is the
; "Preparing to Install" page with its list of applications and two radio buttons - a question whose only
; sensible answer is yes, in front of somebody who just wants the update. The app cooperates: since the
; close handler honours CloseReason, the restart manager's request ends the process properly (stopping the
; Cast sessions) instead of being swallowed by "minimize to tray" and then killed.
CloseApplications=force
; NOT restarted by the restart manager: the [Run] entry below already starts KlangHub, and with the
; language setup just chose. Doing both left two instances running after every upgrade - and the restart
; manager's copy would carry the OLD command line, so they would not even agree on the language.
RestartApplications=no

[Languages]
; The language NAME is the ISO code on purpose - {language} is passed straight to KlangHub as --lang=.
; Irish (ga) and Maltese (mt) have no Inno translation; those users get an English installer and can switch
; the app itself to their language in Einstellungen - the app ships all 24 EU languages either way.
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "bg"; MessagesFile: "compiler:Languages\Bulgarian.isl"
Name: "cs"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "da"; MessagesFile: "compiler:Languages\Danish.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "el"; MessagesFile: "compiler:Languages\Greek.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "et"; MessagesFile: "compiler:Languages\Estonian.isl"
Name: "fi"; MessagesFile: "compiler:Languages\Finnish.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "hr"; MessagesFile: "compiler:Languages\Croatian.isl"
Name: "hu"; MessagesFile: "compiler:Languages\Hungarian.isl"
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "lt"; MessagesFile: "compiler:Languages\Lithuanian.isl"
Name: "lv"; MessagesFile: "compiler:Languages\Latvian.isl"
Name: "nl"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "pl"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "ro"; MessagesFile: "compiler:Languages\Romanian.isl"
Name: "sk"; MessagesFile: "compiler:Languages\Slovak.isl"
Name: "sl"; MessagesFile: "compiler:Languages\Slovenian.isl"
Name: "sv"; MessagesFile: "compiler:Languages\Swedish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The whole publish tree, including every satellite assembly (one folder per language).
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; MIT requires the licence to travel with the software - it is installed as a readable text file rather
; than shown as a click-through nobody reads.
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\docs\THIRD-PARTY-LICENSES.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; Start in the language setup just used, not in whatever Windows happens to be set to.
Filename: "{app}\{#AppExe}"; Parameters: "--lang={code:SelectedAppLanguage}"; \
  Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\{#AppName}"

[CustomMessages]
; Shown instead of Inno's "Preparing to Install" page when KlangHub is running - one sentence
; that says what will happen, rather than a list of applications and two radio buttons.
en.ClosingTitle=KlangHub is running
en.ClosingText=KlangHub is still running. Setup will close it now, install the update and start it again afterwards.
de.ClosingTitle=KlangHub läuft noch
de.ClosingText=KlangHub läuft noch. Setup beendet die App jetzt, installiert die Aktualisierung und startet sie danach wieder.
fr.ClosingTitle=KlangHub est en cours d’exécution
fr.ClosingText=KlangHub est encore ouvert. Le programme d’installation va le fermer, installer la mise à jour et le relancer ensuite.
es.ClosingTitle=KlangHub está en ejecución
es.ClosingText=KlangHub sigue abierto. El instalador lo cerrará ahora, instalará la actualización y volverá a abrirlo después.
it.ClosingTitle=KlangHub è in esecuzione
it.ClosingText=KlangHub è ancora aperto. L’installazione lo chiuderà ora, installerà l’aggiornamento e lo riavvierà al termine.
nl.ClosingTitle=KlangHub is nog actief
nl.ClosingText=KlangHub is nog actief. Setup sluit de app nu, installeert de update en start de app daarna opnieuw.
pt.ClosingTitle=O KlangHub está em execução
pt.ClosingText=O KlangHub ainda está aberto. A instalação vai fechá-lo, instalar a atualização e voltar a abri-lo no fim.
pl.ClosingTitle=KlangHub jest uruchomiony
pl.ClosingText=KlangHub jest nadal uruchomiony. Instalator zamknie aplikację, zainstaluje aktualizację i uruchomi ją ponownie.
sv.ClosingTitle=KlangHub körs
sv.ClosingText=KlangHub körs fortfarande. Setup stänger appen nu, installerar uppdateringen och startar den igen efteråt.
da.ClosingTitle=KlangHub kører
da.ClosingText=KlangHub kører stadig. Setup lukker appen nu, installerer opdateringen og starter den igen bagefter.
fi.ClosingTitle=KlangHub on käynnissä
fi.ClosingText=KlangHub on yhä käynnissä. Asennus sulkee sovelluksen, asentaa päivityksen ja käynnistää sen sitten uudelleen.
et.ClosingTitle=KlangHub töötab
et.ClosingText=KlangHub on veel avatud. Häälestus sulgeb rakenduse, paigaldab uuenduse ja käivitab selle seejärel uuesti.
lv.ClosingTitle=KlangHub darbojas
lv.ClosingText=KlangHub joprojām darbojas. Uzstādīšana to aizvērs, instalēs atjauninājumu un pēc tam palaidīs no jauna.
lt.ClosingTitle=„KlangHub“ veikia
lt.ClosingText=„KlangHub“ vis dar veikia. Diegimo programa uždarys programą, įdiegs naujinį ir paskui ją paleis iš naujo.
el.ClosingTitle=Το KlangHub εκτελείται
el.ClosingText=Το KlangHub εκτελείται ακόμη. Η εγκατάσταση θα το κλείσει τώρα, θα εγκαταστήσει την ενημέρωση και θα το ανοίξει ξανά.
cs.ClosingTitle=KlangHub běží
cs.ClosingText=KlangHub je stále spuštěný. Instalace aplikaci nyní zavře, nainstaluje aktualizaci a poté ji znovu spustí.
sk.ClosingTitle=KlangHub je spustený
sk.ClosingText=KlangHub je stále spustený. Inštalácia aplikáciu teraz zavrie, nainštaluje aktualizáciu a potom ju znova spustí.
sl.ClosingTitle=KlangHub se izvaja
sl.ClosingText=KlangHub še vedno teče. Namestitev bo aplikacijo zdaj zaprla, namestila posodobitev in jo nato znova zagnala.
hr.ClosingTitle=KlangHub je pokrenut
hr.ClosingText=KlangHub je još uvijek pokrenut. Instalacija će aplikaciju sada zatvoriti, instalirati ažuriranje i zatim je ponovno pokrenuti.
hu.ClosingTitle=A KlangHub fut
hu.ClosingText=A KlangHub még fut. A telepítő most bezárja az alkalmazást, telepíti a frissítést, majd újraindítja.
ro.ClosingTitle=KlangHub rulează
ro.ClosingText=KlangHub rulează încă. Programul de instalare îl va închide acum, va instala actualizarea și îl va porni din nou.
bg.ClosingTitle=KlangHub работи
bg.ClosingText=KlangHub все още работи. Инсталацията ще затвори приложението, ще инсталира обновлението и ще го стартира отново.
; The wizard's own language page. One line per installer language - Inno needs the active language covered.
en.LangPageTitle=Language
en.LangPageSubtitle=Which language should KlangHub speak?
en.LangPageInfo=Choose the language for the app. You can change it any time in Settings.
de.LangPageTitle=Sprache
de.LangPageSubtitle=In welcher Sprache soll KlangHub sprechen?
de.LangPageInfo=Wähle die Sprache der App. Du kannst sie jederzeit in den Einstellungen ändern.
fr.LangPageTitle=Langue
fr.LangPageSubtitle=Dans quelle langue KlangHub doit-il parler ?
fr.LangPageInfo=Choisis la langue de l'application. Tu peux la changer à tout moment dans les réglages.
es.LangPageTitle=Idioma
es.LangPageSubtitle=¿En qué idioma debe hablar KlangHub?
es.LangPageInfo=Elige el idioma de la aplicación. Puedes cambiarlo cuando quieras en los ajustes.
it.LangPageTitle=Lingua
it.LangPageSubtitle=In che lingua deve parlare KlangHub?
it.LangPageInfo=Scegli la lingua dell'app. Puoi cambiarla quando vuoi nelle impostazioni.
nl.LangPageTitle=Taal
nl.LangPageSubtitle=In welke taal moet KlangHub spreken?
nl.LangPageInfo=Kies de taal van de app. Je kunt dit altijd wijzigen in de instellingen.
pt.LangPageTitle=Idioma
pt.LangPageSubtitle=Em que idioma deve falar o KlangHub?
pt.LangPageInfo=Escolhe o idioma da aplicação. Podes alterá-lo quando quiseres nas definições.
pl.LangPageTitle=Język
pl.LangPageSubtitle=W jakim języku ma mówić KlangHub?
pl.LangPageInfo=Wybierz język aplikacji. Można go zmienić w każdej chwili w ustawieniach.
sv.LangPageTitle=Språk
sv.LangPageSubtitle=Vilket språk ska KlangHub tala?
sv.LangPageInfo=Välj språk för appen. Du kan ändra det när som helst i inställningarna.
da.LangPageTitle=Sprog
da.LangPageSubtitle=Hvilket sprog skal KlangHub tale?
da.LangPageInfo=Vælg appens sprog. Du kan altid ændre det i indstillingerne.
fi.LangPageTitle=Kieli
fi.LangPageSubtitle=Millä kielellä KlangHub puhuu?
fi.LangPageInfo=Valitse sovelluksen kieli. Voit vaihtaa sen milloin tahansa asetuksista.
et.LangPageTitle=Keel
et.LangPageSubtitle=Mis keeles KlangHub räägib?
et.LangPageInfo=Vali rakenduse keel. Saad seda seadetes igal ajal muuta.
lv.LangPageTitle=Valoda
lv.LangPageSubtitle=Kādā valodā KlangHub runās?
lv.LangPageInfo=Izvēlies lietotnes valodu. To vari mainīt jebkurā brīdī iestatījumos.
lt.LangPageTitle=Kalba
lt.LangPageSubtitle=Kokia kalba kalbės KlangHub?
lt.LangPageInfo=Pasirink programos kalbą. Ją bet kada gali pakeisti nustatymuose.
el.LangPageTitle=Γλώσσα
el.LangPageSubtitle=Σε ποια γλώσσα θα μιλάει το KlangHub;
el.LangPageInfo=Διάλεξε τη γλώσσα της εφαρμογής. Μπορείς να την αλλάξεις όποτε θες στις ρυθμίσεις.
cs.LangPageTitle=Jazyk
cs.LangPageSubtitle=Jakým jazykem má KlangHub mluvit?
cs.LangPageInfo=Vyber jazyk aplikace. Kdykoli jej lze změnit v nastavení.
sk.LangPageTitle=Jazyk
sk.LangPageSubtitle=Akým jazykom má KlangHub hovoriť?
sk.LangPageInfo=Vyber jazyk aplikácie. Kedykoľvek ho možno zmeniť v nastaveniach.
sl.LangPageTitle=Jezik
sl.LangPageSubtitle=V katerem jeziku naj govori KlangHub?
sl.LangPageInfo=Izberi jezik aplikacije. Kadar koli ga lahko spremeniš v nastavitvah.
hr.LangPageTitle=Jezik
hr.LangPageSubtitle=Kojim jezikom neka govori KlangHub?
hr.LangPageInfo=Odaberi jezik aplikacije. Možeš ga promijeniti bilo kada u postavkama.
hu.LangPageTitle=Nyelv
hu.LangPageSubtitle=Milyen nyelven szóljon a KlangHub?
hu.LangPageInfo=Válaszd ki az alkalmazás nyelvét. Bármikor módosíthatod a beállításokban.
ro.LangPageTitle=Limbă
ro.LangPageSubtitle=În ce limbă să vorbească KlangHub?
ro.LangPageInfo=Alege limba aplicației. O poți schimba oricând în setări.
bg.LangPageTitle=Език
bg.LangPageSubtitle=На какъв език да говори KlangHub?
bg.LangPageInfo=Избери езика на приложението. Можеш да го смениш по всяко време в настройките.

en.RuntimeTitle=Microsoft .NET Desktop Runtime
en.RuntimeInfo=KlangHub needs the Microsoft .NET Desktop Runtime. Setup will download and install it now (about 57 MB).
en.RuntimeFailed=The .NET Desktop Runtime could not be installed. KlangHub will not start without it. You can install it manually from https://dotnet.microsoft.com/download
de.RuntimeTitle=Microsoft .NET Desktop Runtime
de.RuntimeInfo=KlangHub benötigt die Microsoft .NET Desktop Runtime. Setup lädt sie jetzt herunter und installiert sie (etwa 57 MB).
de.RuntimeFailed=Die .NET Desktop Runtime konnte nicht installiert werden. Ohne sie startet KlangHub nicht. Du kannst sie manuell installieren: https://dotnet.microsoft.com/download
fr.RuntimeTitle=Microsoft .NET Desktop Runtime
fr.RuntimeInfo=KlangHub a besoin du Microsoft .NET Desktop Runtime. Le programme d'installation va le télécharger et l'installer (environ 57 Mo).
fr.RuntimeFailed=Le .NET Desktop Runtime n'a pas pu être installé. KlangHub ne démarrera pas sans lui : https://dotnet.microsoft.com/download
es.RuntimeTitle=Microsoft .NET Desktop Runtime
es.RuntimeInfo=KlangHub necesita Microsoft .NET Desktop Runtime. El instalador lo descargará e instalará ahora (unos 57 MB).
es.RuntimeFailed=No se pudo instalar .NET Desktop Runtime. KlangHub no se iniciará sin él: https://dotnet.microsoft.com/download
it.RuntimeTitle=Microsoft .NET Desktop Runtime
it.RuntimeInfo=KlangHub richiede Microsoft .NET Desktop Runtime. Il programma di installazione lo scaricherà e installerà ora (circa 57 MB).
it.RuntimeFailed=Impossibile installare .NET Desktop Runtime. Senza di esso KlangHub non si avvia: https://dotnet.microsoft.com/download
nl.RuntimeTitle=Microsoft .NET Desktop Runtime
nl.RuntimeInfo=KlangHub heeft Microsoft .NET Desktop Runtime nodig. Setup downloadt en installeert die nu (ongeveer 57 MB).
nl.RuntimeFailed=De .NET Desktop Runtime kon niet worden geïnstalleerd. Zonder deze start KlangHub niet: https://dotnet.microsoft.com/download
pt.RuntimeTitle=Microsoft .NET Desktop Runtime
pt.RuntimeInfo=O KlangHub precisa do Microsoft .NET Desktop Runtime. A instalação vai transferi-lo e instalá-lo agora (cerca de 57 MB).
pt.RuntimeFailed=Não foi possível instalar o .NET Desktop Runtime. Sem ele o KlangHub não arranca: https://dotnet.microsoft.com/download
pl.RuntimeTitle=Microsoft .NET Desktop Runtime
pl.RuntimeInfo=KlangHub wymaga Microsoft .NET Desktop Runtime. Instalator pobierze go i zainstaluje teraz (około 57 MB).
pl.RuntimeFailed=Nie udało się zainstalować .NET Desktop Runtime. Bez niego KlangHub nie uruchomi się: https://dotnet.microsoft.com/download
sv.RuntimeTitle=Microsoft .NET Desktop Runtime
sv.RuntimeInfo=KlangHub behöver Microsoft .NET Desktop Runtime. Installationen hämtar och installerar den nu (cirka 57 MB).
sv.RuntimeFailed=Det gick inte att installera .NET Desktop Runtime. Utan den startar inte KlangHub: https://dotnet.microsoft.com/download
da.RuntimeTitle=Microsoft .NET Desktop Runtime
da.RuntimeInfo=KlangHub kræver Microsoft .NET Desktop Runtime. Installationen henter og installerer den nu (cirka 57 MB).
da.RuntimeFailed=Kunne ikke installere .NET Desktop Runtime. Uden den starter KlangHub ikke: https://dotnet.microsoft.com/download
fi.RuntimeTitle=Microsoft .NET Desktop Runtime
fi.RuntimeInfo=KlangHub tarvitsee Microsoft .NET Desktop Runtimen. Asennus lataa ja asentaa sen nyt (noin 57 Mt).
fi.RuntimeFailed=.NET Desktop Runtimen asennus epäonnistui. Ilman sitä KlangHub ei käynnisty: https://dotnet.microsoft.com/download
et.RuntimeTitle=Microsoft .NET Desktop Runtime
et.RuntimeInfo=KlangHub vajab Microsoft .NET Desktop Runtime'i. Paigaldaja laadib selle nüüd alla ja paigaldab (umbes 57 MB).
et.RuntimeFailed=.NET Desktop Runtime'i ei õnnestunud paigaldada. Ilma selleta KlangHub ei käivitu: https://dotnet.microsoft.com/download
lv.RuntimeTitle=Microsoft .NET Desktop Runtime
lv.RuntimeInfo=KlangHub nepieciešams Microsoft .NET Desktop Runtime. Instalētājs to tagad lejupielādēs un uzstādīs (aptuveni 57 MB).
lv.RuntimeFailed=Neizdevās uzstādīt .NET Desktop Runtime. Bez tā KlangHub nedarbosies: https://dotnet.microsoft.com/download
lt.RuntimeTitle=Microsoft .NET Desktop Runtime
lt.RuntimeInfo=KlangHub reikia Microsoft .NET Desktop Runtime. Diegimo programa ją dabar atsisiųs ir įdiegs (apie 57 MB).
lt.RuntimeFailed=Nepavyko įdiegti .NET Desktop Runtime. Be jos KlangHub nepasileis: https://dotnet.microsoft.com/download
el.RuntimeTitle=Microsoft .NET Desktop Runtime
el.RuntimeInfo=Το KlangHub χρειάζεται το Microsoft .NET Desktop Runtime. Η εγκατάσταση θα το κατεβάσει και θα το εγκαταστήσει τώρα (περίπου 57 MB).
el.RuntimeFailed=Δεν ήταν δυνατή η εγκατάσταση του .NET Desktop Runtime. Χωρίς αυτό το KlangHub δεν ξεκινά: https://dotnet.microsoft.com/download
cs.RuntimeTitle=Microsoft .NET Desktop Runtime
cs.RuntimeInfo=KlangHub potřebuje Microsoft .NET Desktop Runtime. Instalátor jej nyní stáhne a nainstaluje (přibližně 57 MB).
cs.RuntimeFailed=.NET Desktop Runtime se nepodařilo nainstalovat. Bez něj se KlangHub nespustí: https://dotnet.microsoft.com/download
sk.RuntimeTitle=Microsoft .NET Desktop Runtime
sk.RuntimeInfo=KlangHub potrebuje Microsoft .NET Desktop Runtime. Inštalátor ho teraz stiahne a nainštaluje (približne 57 MB).
sk.RuntimeFailed=.NET Desktop Runtime sa nepodarilo nainštalovať. Bez neho sa KlangHub nespustí: https://dotnet.microsoft.com/download
sl.RuntimeTitle=Microsoft .NET Desktop Runtime
sl.RuntimeInfo=KlangHub potrebuje Microsoft .NET Desktop Runtime. Namestitev ga bo zdaj prenesla in namestila (približno 57 MB).
sl.RuntimeFailed=.NET Desktop Runtime ni bilo mogoče namestiti. Brez njega se KlangHub ne zažene: https://dotnet.microsoft.com/download
hr.RuntimeTitle=Microsoft .NET Desktop Runtime
hr.RuntimeInfo=KlangHub treba Microsoft .NET Desktop Runtime. Instalacija će ga sada preuzeti i instalirati (oko 57 MB).
hr.RuntimeFailed=.NET Desktop Runtime nije bilo moguće instalirati. Bez njega se KlangHub neće pokrenuti: https://dotnet.microsoft.com/download
hu.RuntimeTitle=Microsoft .NET Desktop Runtime
hu.RuntimeInfo=A KlangHubhoz szükséges a Microsoft .NET Desktop Runtime. A telepítő most letölti és telepíti (körülbelül 57 MB).
hu.RuntimeFailed=A .NET Desktop Runtime telepítése nem sikerült. Enélkül a KlangHub nem indul el: https://dotnet.microsoft.com/download
ro.RuntimeTitle=Microsoft .NET Desktop Runtime
ro.RuntimeInfo=KlangHub are nevoie de Microsoft .NET Desktop Runtime. Programul de instalare îl va descărca și instala acum (aproximativ 57 MB).
ro.RuntimeFailed=Nu s-a putut instala .NET Desktop Runtime. Fără el, KlangHub nu pornește: https://dotnet.microsoft.com/download
bg.RuntimeTitle=Microsoft .NET Desktop Runtime
bg.RuntimeInfo=KlangHub изисква Microsoft .NET Desktop Runtime. Инсталаторът ще го изтегли и инсталира сега (около 57 MB).
bg.RuntimeFailed=Microsoft .NET Desktop Runtime не можа да бъде инсталиран. Без него KlangHub няма да стартира: https://dotnet.microsoft.com/download

[Code]
{ ------------------------------------------------------------------------------------------------
  The wizard wears KlangHub's own palette instead of the grey Windows default: deep ink surfaces,
  ivory text, one amber accent. Inno draws its pages with ordinary VCL controls, so the theme is
  applied by walking the form once and recolouring every control it owns - including the ones on
  pages that are created later, which is why ThemeAll runs again on each page change.
  ------------------------------------------------------------------------------------------------ }

const
  clInk      = $14100E;  { #0E1014 - Pascal colours are $BBGGRR }
  clInk2     = $110D0B;  { #0B0D11 }
  clSurface  = $231B17;  { #171B23 }
  clLine     = $3D322A;  { #2A323D }
  clIvory    = $DDECF3;  { #F3ECDD }
  clSlate    = $A69A8A;  { #8A9AA6 }
  clAmber    = $5AB6E8;  { #E8B65A }

{ --- native title bar in the app's colours: the same DWM attributes MainForm sets --- }
function DwmSetWindowAttribute(hwnd: HWND; attr: Integer; var value: Integer; size: Integer): Integer;
  external 'DwmSetWindowAttribute@dwmapi.dll stdcall delayload';

{ the OS draws scroll bars itself; this is the same call MainForm uses to get the dark ones }
function SetWindowTheme(hwnd: HWND; SubAppName: WideString; SubIdList: WideString): Integer;
  external 'SetWindowTheme@uxtheme.dll stdcall delayload';

procedure DarkScrollbars(C: TWinControl);
begin
  try
    SetWindowTheme(C.Handle, 'DarkMode_Explorer', '');
  except
  end;
end;

{ Turns visual styles off for one control: a themed check box or radio button paints its own caption and
  ignores Font.Color, which left the licence choices grey-on-ink. }
procedure PlainTheme(C: TWinControl);
begin
  try
    SetWindowTheme(C.Handle, '', '');
  except
  end;
end;

procedure DarkenTitleBar(H: HWND);
var
  V: Integer;
begin
  try
    V := 1;                        { DWMWA_USE_IMMERSIVE_DARK_MODE }
    DwmSetWindowAttribute(H, 20, V, SizeOf(V));
    V := clInk;                    { DWMWA_CAPTION_COLOR - already $BBGGRR, which is what DWM wants }
    DwmSetWindowAttribute(H, 35, V, SizeOf(V));
    V := clIvory;                  { DWMWA_TEXT_COLOR }
    DwmSetWindowAttribute(H, 36, V, SizeOf(V));
    V := clInk;                    { DWMWA_BORDER_COLOR - caption, border and client read as one shell }
    DwmSetWindowAttribute(H, 34, V, SizeOf(V));
  except
    { older Windows builds simply keep the light caption }
  end;
end;

procedure ThemeControl(C: TControl);
begin
  if C is TNewStaticText then
  begin
    TNewStaticText(C).Color := clInk;
    TNewStaticText(C).Font.Color := clIvory;
  end
  else if C is TNewCheckListBox then
  begin
    TNewCheckListBox(C).Color := clInk2;
    TNewCheckListBox(C).Font.Color := clIvory;
    DarkScrollbars(TNewCheckListBox(C));
  end
  else if C is TNewMemo then
  begin
    TNewMemo(C).Color := clInk2;
    TNewMemo(C).Font.Color := clIvory;
    DarkScrollbars(TNewMemo(C));
  end
  else if C is TNewEdit then
  begin
    TNewEdit(C).Color := clInk2;
    TNewEdit(C).Font.Color := clIvory;
  end
  else if C is TNewComboBox then
  begin
    TNewComboBox(C).Color := clInk2;
    TNewComboBox(C).Font.Color := clIvory;
  end
  else if C is TNewListBox then
  begin
    TNewListBox(C).Color := clInk2;
    TNewListBox(C).Font.Color := clIvory;
    DarkScrollbars(TNewListBox(C));
  end
  else if C is TRichEditViewer then
  begin
    { Only the background here: assigning Font.Color re-formats the whole document and wipes out the
      colours the RTF brought with it (see installer/make-license-rtf.ps1), which left the licence
      near-black on ink. The text colour comes from the RTF's own colour table. }
    TRichEditViewer(C).Color := clInk2;
    DarkScrollbars(TRichEditViewer(C));
  end
  else if C is TCheckBox then
  begin
    TCheckBox(C).Color := clInk;
    TCheckBox(C).Font.Color := clIvory;
    PlainTheme(TCheckBox(C));
  end
  else if C is TRadioButton then
  begin
    TRadioButton(C).Color := clInk;
    TRadioButton(C).Font.Color := clIvory;
    PlainTheme(TRadioButton(C));
  end
  else if C is TLabel then
  begin
    TLabel(C).Color := clInk;
    TLabel(C).Font.Color := clIvory;
  end
  else if C is TPanel then
  begin
    TPanel(C).Color := clInk;
    TPanel(C).ParentBackground := False;
  end
  else if C is TNewNotebookPage then
    TNewNotebookPage(C).Color := clInk;
  { the progress bar keeps the system rendering on purpose - a themed one cannot be recoloured reliably }
end;

procedure ThemeAll(Parent: TWinControl);
var
  I: Integer;
  C: TControl;
begin
  for I := 0 to Parent.ControlCount - 1 do
  begin
    C := Parent.Controls[I];
    ThemeControl(C);
    if C is TWinControl then
      ThemeAll(TWinControl(C));
  end;
end;

procedure StyleHeadings;
begin
  { the page title reads as the app's own display type, the description stays quiet }
  WizardForm.PageNameLabel.Font.Color := clIvory;
  WizardForm.PageNameLabel.Font.Size := 11;
  WizardForm.PageNameLabel.Font.Style := [fsBold];
  WizardForm.PageDescriptionLabel.Font.Color := clSlate;

  WizardForm.WelcomeLabel1.Font.Color := clIvory;
  WizardForm.WelcomeLabel1.Font.Size := 15;
  WizardForm.WelcomeLabel2.Font.Color := clSlate;
  WizardForm.FinishedHeadingLabel.Font.Color := clIvory;
  WizardForm.FinishedHeadingLabel.Font.Size := 15;
  WizardForm.FinishedLabel.Font.Color := clSlate;

  { one amber accent, on the line that separates the header from the page }
  WizardForm.Bevel.Visible := False;
  WizardForm.Bevel1.Visible := False;
  WizardForm.MainPanel.Color := clInk2;
  WizardForm.InnerPage.Color := clInk;
end;


var
  LangPage: TWizardPage;
  LangCombo: TNewListBox;
  LangCodes: array[0..23] of String;

{ The 24 official EU languages, each under its own name - a language is only findable under its endonym. }
procedure BuildLanguageTable;
begin
  LangCodes[0]  := 'bg'; LangCodes[1]  := 'cs'; LangCodes[2]  := 'da'; LangCodes[3]  := 'de';
  LangCodes[4]  := 'el'; LangCodes[5]  := 'en'; LangCodes[6]  := 'es'; LangCodes[7]  := 'et';
  LangCodes[8]  := 'fi'; LangCodes[9]  := 'fr'; LangCodes[10] := 'ga'; LangCodes[11] := 'hr';
  LangCodes[12] := 'hu'; LangCodes[13] := 'it'; LangCodes[14] := 'lt'; LangCodes[15] := 'lv';
  LangCodes[16] := 'mt'; LangCodes[17] := 'nl'; LangCodes[18] := 'pl'; LangCodes[19] := 'pt';
  LangCodes[20] := 'ro'; LangCodes[21] := 'sk'; LangCodes[22] := 'sl'; LangCodes[23] := 'sv';

  LangCombo.Items.Add('Български');   LangCombo.Items.Add('Čeština');
  LangCombo.Items.Add('Dansk');       LangCombo.Items.Add('Deutsch');
  LangCombo.Items.Add('Ελληνικά');    LangCombo.Items.Add('English');
  LangCombo.Items.Add('Español');     LangCombo.Items.Add('Eesti');
  LangCombo.Items.Add('Suomi');       LangCombo.Items.Add('Français');
  LangCombo.Items.Add('Gaeilge');     LangCombo.Items.Add('Hrvatski');
  LangCombo.Items.Add('Magyar');      LangCombo.Items.Add('Italiano');
  LangCombo.Items.Add('Lietuvių');    LangCombo.Items.Add('Latviešu');
  LangCombo.Items.Add('Malti');       LangCombo.Items.Add('Nederlands');
  LangCombo.Items.Add('Polski');      LangCombo.Items.Add('Português');
  LangCombo.Items.Add('Română');      LangCombo.Items.Add('Slovenčina');
  LangCombo.Items.Add('Slovenščina'); LangCombo.Items.Add('Svenska');
end;

procedure CreateLanguagePage;
var
  Info: TNewStaticText;
  I, Sel: Integer;
begin
  LangPage := CreateCustomPage(wpWelcome, ExpandConstant('{cm:LangPageTitle}'),
                               ExpandConstant('{cm:LangPageSubtitle}'));

  Info := TNewStaticText.Create(LangPage);
  Info.Parent := LangPage.Surface;
  Info.Left := 0;
  Info.Top := 0;
  Info.Width := LangPage.SurfaceWidth;
  Info.WordWrap := True;
  Info.AutoSize := True;
  Info.Caption := ExpandConstant('{cm:LangPageInfo}');

  LangCombo := TNewListBox.Create(LangPage);
  LangCombo.Parent := LangPage.Surface;
  LangCombo.Left := 0;
  LangCombo.Top := Info.Top + Info.Height + ScaleY(18);
  LangCombo.Width := ScaleX(280);
  LangCombo.Height := LangPage.SurfaceHeight - LangCombo.Top - ScaleY(6);
  LangCombo.BorderStyle := bsNone;
  BuildLanguageTable;

  { pre-select what setup itself detected, so the common case is just "Next" }
  Sel := 5; { en }
  for I := 0 to 23 do
    if LangCodes[I] = ActiveLanguage then
      Sel := I;
  LangCombo.ItemIndex := Sel;
end;

// Referenced from [Run] via the code: constant - the language KlangHub is started in.
function SelectedAppLanguage(Param: String): String;
begin
  if (LangCombo <> nil) and (LangCombo.ItemIndex >= 0) then
    Result := LangCodes[LangCombo.ItemIndex]
  else
    Result := ActiveLanguage;
end;

{ --- .NET Desktop Runtime: detect, and fetch it if the machine has none --- }
const
  RuntimeUrl = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe';

var
  DownloadPage: TDownloadWizardPage;

function DesktopRuntimeInstalled: Boolean;
var
  Root: String;
  FindRec: TFindRec;
begin
  { The shared framework lives in one folder per version. KlangHub targets net10.0-windows, so any
    Microsoft.WindowsDesktop.App\10.* will host it. }
  Result := False;
  Root := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(Root) then
    Exit;
  if FindFirst(Root + '\10.*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          Result := True;
          Break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

{ Runs on the "ready" page: download, then hand over to Microsoft's own installer. It asks for elevation
  itself (the runtime is machine-wide), which is why setup does not need admin rights of its own. }
function EnsureRuntime: Boolean;
var
  ResultCode: Integer;
  Target: String;
begin
  Result := True;
  if DesktopRuntimeInstalled then
    Exit;

  Target := ExpandConstant('{tmp}\windowsdesktop-runtime.exe');
  DownloadPage.Clear;
  DownloadPage.Add(RuntimeUrl, 'windowsdesktop-runtime.exe', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
      if not ShellExec('runas', Target, '/install /passive /norestart', '',
                       SW_SHOW, ewWaitUntilTerminated, ResultCode) then
        Result := False
      else if (ResultCode <> 0) and (ResultCode <> 3010) and (ResultCode <> 1641) then
        Result := False;
    except
      Result := False;
    end;
  finally
    DownloadPage.Hide;
  end;

  if not Result then
    Result := SuppressibleMsgBox(ExpandConstant('{cm:RuntimeFailed}'), mbError, MB_OKCANCEL, IDOK) = IDOK;
end;

{ Is KlangHub running? Asked of the process list rather than a window title, so it also finds an
  instance that has hidden itself in the notification area. }
function KlangHubIsRunning: Boolean;
var
  code: Integer;
begin
  Result := Exec(ExpandConstant('{cmd}'),
                 '/C tasklist /FI "IMAGENAME eq {#AppExe}" /NH | find /I "{#AppExe}" >nul',
                 '', SW_HIDE, ewWaitUntilTerminated, code) and (code = 0);
end;

{
  Closes a running KlangHub before the files are replaced.

  Inno's own restart manager asks the window to close, which a KlangHub set to "minimize to tray"
  answers by hiding - so the close fails and the user is shown the "Preparing to Install" page: a list
  of applications, an error icon and two radio buttons, in front of somebody who only wanted the
  update. This does the job first, and says in one sentence what it is doing.

  Polite first: taskkill without /F posts WM_CLOSE, which ends an ordinary KlangHub properly - stopping
  its Cast sessions on the way out, so no television is left sitting on the Cast logo. Only an instance
  that is still there after that is ended the hard way.
}
procedure CloseRunningKlangHub;
var
  code, waited: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /IM "{#AppExe}" >nul 2>&1', '', SW_HIDE,
       ewWaitUntilTerminated, code);

  waited := 0;
  while (waited < 5000) and KlangHubIsRunning do
  begin
    Sleep(250);
    waited := waited + 250;
  end;

  if KlangHubIsRunning then
    Exec(ExpandConstant('{cmd}'), '/C taskkill /F /IM "{#AppExe}" >nul 2>&1', '', SW_HIDE,
         ewWaitUntilTerminated, code);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpReady then
  begin
    if KlangHubIsRunning then
    begin
      { Told, not asked: there is only one sensible answer, and the wizard already has a Cancel button. }
      MsgBox(ExpandConstant('{cm:ClosingText}'), mbInformation, MB_OK);
      CloseRunningKlangHub;
    end;
    Result := EnsureRuntime;
  end;
end;

procedure InitializeWizard();
begin
  WizardForm.Color := clInk;
  WizardForm.Font.Name := 'Segoe UI';
  WizardForm.Font.Color := clIvory;
  CreateLanguagePage;
  DownloadPage := CreateDownloadPage(ExpandConstant('{cm:RuntimeTitle}'), ExpandConstant('{cm:RuntimeInfo}'),
                                     @OnDownloadProgress);
  ThemeAll(WizardForm);
  StyleHeadings;
  DarkenTitleBar(WizardForm.Handle);
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  { pages build their controls lazily - re-apply so nothing shows up in Windows grey }
  ThemeAll(WizardForm);
  StyleHeadings;

  { The "preparing" page should never appear now that CloseApplications=force does the job silently. If it
    ever does - a process that will not close - it must at least be readable: its radio buttons and list
    are created by Inno itself and came out in Windows grey on ink. }
  if CurPageID = wpPreparing then
  begin
    WizardForm.PreparingLabel.Font.Color := clIvory;
    WizardForm.PreparingYesRadio.Font.Color := clIvory;
    WizardForm.PreparingNoRadio.Font.Color := clIvory;
    WizardForm.PreparingYesRadio.Color := clInk;
    WizardForm.PreparingNoRadio.Color := clInk;
    PlainTheme(WizardForm.PreparingYesRadio);
    PlainTheme(WizardForm.PreparingNoRadio);
    WizardForm.PreparingMemo.Color := clInk2;
    WizardForm.PreparingMemo.Font.Color := clIvory;
    DarkScrollbars(WizardForm.PreparingMemo);
  end;
end;

procedure InitializeUninstallProgressForm();
begin
  { the uninstaller wears the same colours - anything else would look like a different product }
  UninstallProgressForm.Color := clInk;
  UninstallProgressForm.Font.Color := clIvory;
  ThemeAll(UninstallProgressForm);
  DarkenTitleBar(UninstallProgressForm.Handle);
end;
