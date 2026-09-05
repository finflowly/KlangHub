# Sicherheit

## Eine Lücke melden

Bitte **kein öffentliches Issue** eröffnen. Melde Sicherheitslücken über die GitHub Security
Advisories des Repositories:

**<https://github.com/finflowly/KlangHub/security/advisories/new>**

(Auf GitHub: Reiter *Security* → *Report a vulnerability*.) Der Bericht ist nur für die Maintainer
sichtbar, bis eine Behebung veröffentlicht ist.

Hilfreich sind: eine Beschreibung, was ein Angreifer damit erreichen kann, die Schritte zum
Nachstellen, die betroffene Version und – falls vorhanden – ein Vorschlag zur Behebung. Bitte keine
echten Adressen, Gerätelisten oder Protokolldateien mitschicken; ersetze sie durch Beispielwerte.

Eine Rückmeldung gibt es, sobald der Bericht gesichtet ist. Dies ist ein Freizeitprojekt – es gibt
keine zugesicherte Reaktionszeit und kein Bug-Bounty.

## Unterstützte Versionen

Behoben wird jeweils in der neuesten Version. Ältere Versionen erhalten keine Rückportierungen.

## Womit KlangHub im Netzwerk umgeht

Zur Einordnung, was überhaupt angreifbar ist:

- KlangHub öffnet einen **HTTP-Server im lokalen Netz**, von dem die Cast-Empfänger den Audiostrom
  abholen. Er ist unverschlüsselt und nicht authentifiziert – so verlangt es das Cast-Protokoll,
  und deshalb gehört KlangHub in ein vertrauenswürdiges Heimnetz, nicht ins offene Internet.
- Geräte werden per **mDNS** im lokalen Netz gefunden.
- Die Steuerverbindung zum Empfänger läuft über **TLS auf Port 8009**; das Gerätezertifikat wird
  dabei nicht geprüft, weil Cast-Geräte Zertifikate verwenden, die kein öffentlicher Trust Store
  kennt.
- KlangHub sendet keine Daten an Dritte, verlangt kein Konto und legt keine Telemetrie an.
