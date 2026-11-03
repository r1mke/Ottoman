# Preporuke (recommender) — kako radi

Kod: `src/Ottoman.Domain/Recommendations/` (čista logika, bez baze) i `src/Ottoman.Infrastructure/Recommendations/` (učitavanje podataka + keš).
Testovi: `tests/Ottoman.Domain.Tests/RecommenderTests.cs` i `RecommenderExplanationTests.cs` (determinističke fixture-e; cijeli projekat `Ottoman.Domain.Tests` ima 182 testa), `tests/Ottoman.Application.Tests/ReasonTextTests.cs` (tekst objašnjenja) i `tests/Ottoman.Api.IntegrationTests/RecommendationTests.cs` (uživo nad bazom).

Isti algoritam radi **tri puta**: nad proizvodima, uslugama i frizerima. Metoda je *content-based*: stavke se porede po sadržaju (riječi, kategorija, brend, cijena), a ne po tome šta su kupili drugi ljudi. Za salon s malo korisnika to je pouzdanije od kolaborativnog filtriranja (nema dovoljno podataka za „slične korisnike“).

## 1. Normalizacija teksta
`TextNormalizer` pretvara tekst u male riječi bez dijakritika (č ć š ž đ → c c s z d), razdvaja po znakovima koji nisu slova/brojevi i izbacuje stop-riječi (i, za, na, je, ...). Tako „Šišanje“ i „sisanje“ daju isti token.

## 2. Vektor stavke (TF-IDF + fiksne značajke)
Za svaku stavku se od `naziv + opis` prave težine riječi:

```
tf  = 1 + ln(broj pojavljivanja riječi u stavci)
idf = ln((N + 1) / (broj stavki koje sadrže riječ + 1)) + 1
w(riječ) = tf * idf
```
Rijetke riječi („fade“) važe više od čestih („vosak“). Vektor riječi se normalizuje na dužinu 1, pa se dodaju fiksne značajke:

| Značajka | Težina |
|---|---|
| ista **kategorija** | 1.5 |
| isti **brend** | 1.0 |
| isti **cjenovni razred** (donja/srednja/gornja trećina kataloga) | 0.5 |

## 3. Profil korisnika
Zbir vektora stavki s kojima je korisnik imao veze, svaki pomnožen težinom i **opadanjem u vremenu**:

| Događaj | Težina |
|---|---|
| kupovina proizvoda (narudžba zaprimljena/preuzeta) | +1.0 |
| završen/potvrđen termin s uslugom ili frizerom | +1.0 |
| recenzija 4–5 ★ | +1.5 |
| recenzija 3 ★ | 0 |
| recenzija 1–2 ★ | **−2.0** (nadjačava kupovinu: „kupio i omrznuo“ = neto negativno) |

```
decay = 0.5 ^ (starost_u_danima / 90)      // poluvrijeme 90 dana
```

## 4. Skor i izbor
`skor(stavka) = cos(profil, vektor_stavke)`. Vraća se top-N stavki sa skorom > 0, uz filtere:
- samo **dostupno** (aktivno; proizvodi i na zalihi) — zaliha se čita svježe pri svakom zahtjevu, ne iz keša;
- proizvodi koje je korisnik već kupio se ne nude ponovo (usluge i frizeri se mogu ponoviti).

Izjednačenja se razrješavaju po id-u, pa je rezultat uvijek isti za iste ulaze.

## 5. „Hladni start“
Ako korisnik nema historiju (ili ništa ne daje pozitivan skor), vraća se lista **najpopularnijeg**: broj prodaja/termina + prosječna ocjena. Stavke koje liče na ono što korisniku **nije** odgovaralo ne ulaze: ako je profil zbog loših ocjena (1–2 ★) negativan prema nekoj stavci (kosinus < 0), ona se ne nudi ni kao „popularno“.

## 6. „Slično“ (detalj proizvoda)
`Related(stavka)` = najbliži susjedi te stavke po kosinusu, bez nje same. Zato ulje za bradu daje balzam za bradu, pa ostale proizvode istog brenda.

## 7. Frizeri
Frizer se opisuje svojom titulom, opisom i uslugama koje radi. Profil korisnika se gradi iz termina kod frizera i recenzija. Popularnost = broj termina + prosječna ocjena.

## 8. Objašnjenje uz preporuku („Zašto ovo?“)
Uz svaku preporuku API vraća i rečenicu koja kaže zašto je stavka predložena (`RecommendationReason`: vrsta, naziv izvora i tekst). Nastaje u dva koraka, oba bez baze:

1. **Domen** (`Recommender.ExplainFor`, `ItemIndex.Explain`): od svih stavki s kojima je korisnik imao pozitivan kontakt bira se ona koja najjače „vuče“ kandidata (težina događaja × opadanje u vremenu × kosinus prema kandidatu). Za taj par se zapisuje šta stvarno dijele: istu kategoriju, isti brend, isti cjenovni razred i zajedničke riječi. Ako takve stavke nema, razlog je *Popular*.
2. **Tekst** (`ReasonText.Compose`, `src/Ottoman.Application/Recommendations/ReasonText.cs`) to pretvara u jednu rečenicu na bosanskom:
   - lična preporuka: „Jer ste kupili „Dandy Beard Oil“ - ista kategorija (Njega brade), isti brend (Dandy)“; za usluge „Jer ste koristili uslugu …“, za frizere „Jer ste bili kod frizera …“;
   - „slično“ na detalju proizvoda: „Slično kao „…“ - …“;
   - hladni start: „Popularno u salonu. Čim nešto kupite ili rezervišete, preporuke će biti lične.“

Svaka činjenica u rečenici dolazi iz istog poređenja koje je dalo skor, pa objašnjenje nikad ne tvrdi nešto što nije uticalo na izbor. Mobilna aplikacija tu rečenicu prikazuje uz preporuku.

## 9. Keš i invalidacija
Vektori (skupi dio) se čuvaju u memoriji (`RecommendationCache`). Brišu se **odmah** kad se promijeni katalog: `CatalogChangeInterceptor` presreće svaki `SaveChanges` koji dira proizvod, uslugu, frizera, brend ili kategoriju; operacije koje zaobilaze `SaveChanges` (deaktivacija proizvoda, brisanje brenda/kategorije) pozivaju `Invalidate()` same. Zaliha i ocjene se ne keširaju. Nema TTL-a, pa nema zastarjelih preporuka.

## Primjer računa
Korisnik je kupio *Dandy Beard Oil* (kategorija „Njega brade“, brend Dandy). Profil = vektor tog ulja. Kandidati:

| Stavka | Zašto | Skor |
|---|---|---|
| Proraso Beard Balm | ista kategorija (1.5) + riječi „beard“, „brad“ | najviši |
| Dandy Shaping Pomade | isti brend (1.0) | srednji |
| Dandy Matt Clay | isti brend (1.0) | srednji |
| Šampon protiv peruti | ništa zajedničko | 0 → ne nudi se |

Ulje koje je već kupio je isključeno. To je tačno scenarij iz testa `Beard_oil_is_followed_by_beard_balm_then_other_Dandy_products` i integracijskog testa `Someone_who_bought_beard_oil_is_offered_the_beard_balm_and_not_the_oil_again`.

## Ograničenja (iskreno)
- Nema kolaborativnog signala („kupci koji su kupili X kupili su i Y“); za jedan salon bi bio šum.
- Cjenovni razred ima smisla tek s ≥ 3 stavke s cijenom.
- Riječi se porede doslovno (nema stemminga): „brada“ i „bradu“ su različiti tokeni. Kategorija i brend to djelimično nadoknađuju.
