# Open questions

> Team questions, collected 2026-11-20. Entries are kept word for word in the original language
> (German). Add new questions at the end; when one is answered, write the answer below it (with a
> link to the decision record, if there is one) instead of deleting it.

## Geschäftsmodell und Zielgruppe

- Müssen wir die Serveranschaffung und Heizungsinfrastruktur miteinberechnen? Zahlt das der Kunde
  oder die Firma? Ist das Produkt allgemein mehr kundengerichtet oder admingerichtet?
- Ist das Ziel dass die Berechnungen hauptsächlich auf dem eigenen Server ausgeführt werden oder
  verteilt über alle? Es gibt so verschiedene Arten von Kunden, Privathaushalte die einfach Wärme
  wollen und Unternehmen die private Server wollen, das muss eigentlich sehr separat behandelt
  werden.

## Wärmebedarf und Scheduling

- Wie wissen wir wann ein Haushalt Wärme braucht, weil sie die Heizung anstellen?
- Wieso haben Jobs überhaupt Deadlines? Dann dauert der Request halt etwas länger.

## Sicherheit

- Nicht so wichtig für Hackathon aber sehr interessant: Sicherheitsfrage wenn die Leute den Server
  selbst manipulieren können? Normal sind Datenzentren extrem gesichert.

## Ideen

Wie sind unsere Ideen?

- Einfach eine Cloud bauen, virtuelle CPUS
- Minecraft und andere Game-Server
- Bitcoin minen

Stichworte:

- Thermal-aware cloud pricing
- Heat-demand-aware job auctions

## Added later (English)

- **Trust gap** (from the service brainstorm, 2026-11-20): will customers with confidential data
  accept their jobs running on a server in a private home? Candidate answers in
  [`research/service-brainstorm.md`](research/service-brainstorm.md#the-biggest-risk-the-trust-gap).
