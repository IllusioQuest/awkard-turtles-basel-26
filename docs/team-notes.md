# Team notes

> Team to-dos and open questions, collected 2026-11-20 (merged from `todos.md` and `questions.md`).
> Entries are kept word for word in the original language (German). Tick a to-do when done and
> write your name behind it when you take it. Add new questions at the end of their section; when
> one is answered, write the answer below it (with a link to the decision record, if there is one)
> instead of deleting it. Planned work belongs in the [ticket board](tickets/README.md).

## To-dos

- [ ] Brainstormen: Was führen wir auf den Servern aus? Was lohnt sich, woran ist bedarf, etc.
- [ ] Scheduler der die Jobs verteilt und scheduled (Paper!)
- [ ] Durchrechnen Stromverbrauch von Servern bei versch. lasten und Simulation lokal am Gerät
- [ ] Daten aufbereiten und recherchieren Heizbedarf / Strombedarf von Haushalten
- [ ] Dashboard für die Admins, Parameter tunen und Karte sehen mit Standorten und Strombedarf
- [ ] Dashboard für die Haushalte selbst, Werbung fürs Programm und wo man die Services nutzen kann -
  trennen? Überhaupt nötig?
- [ ] Bonus: Solarenergie und Hitzespeicher hinzufügen

## Open questions

### Geschäftsmodell und Zielgruppe

- Müssen wir die Serveranschaffung und Heizungsinfrastruktur miteinberechnen? Zahlt das der Kunde
  oder die Firma? Ist das Produkt allgemein mehr kundengerichtet oder admingerichtet?
- Ist das Ziel dass die Berechnungen hauptsächlich auf dem eigenen Server ausgeführt werden oder
  verteilt über alle? Es gibt so verschiedene Arten von Kunden, Privathaushalte die einfach Wärme
  wollen und Unternehmen die private Server wollen, das muss eigentlich sehr separat behandelt
  werden.

### Präsentaiton
- Video oder In-Person Vorführung besser?

### Wärmebedarf und Scheduling

- Wie wissen wir wann ein Haushalt Wärme braucht, weil sie die Heizung anstellen?
- Wieso haben Jobs überhaupt Deadlines? Dann dauert der Request halt etwas länger.

### Sicherheit

- Nicht so wichtig für Hackathon aber sehr interessant: Sicherheitsfrage wenn die Leute den Server
  selbst manipulieren können? Normal sind Datenzentren extrem gesichert.

### Ideen

Wie sind unsere Ideen?

- Einfach eine Cloud bauen, virtuelle CPUS
- Minecraft und andere Game-Server
- Bitcoin minen

Stichworte:

- Thermal-aware cloud pricing
- Heat-demand-aware job auctions
