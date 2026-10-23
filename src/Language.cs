using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace VectorFit {
    // Translate display text only. Saved measurements, filenames and driver configuration keep their original values.
    static class L {
        static bool english;
        public static bool English {get{return english;}set{english=value;var culture=new System.Globalization.CultureInfo(value?"en-US":"nb-NO");System.Threading.Thread.CurrentThread.CurrentCulture=culture;System.Threading.Thread.CurrentThread.CurrentUICulture=culture;}}
        public static bool Applying;
        static readonly Dictionary<string,string> translations=Build();
        static readonly Regex pattern=new Regex("(?<![\\p{L}])(?:"+String.Join("|",translations.Keys.OrderByDescending(k=>k.Length).Select(Regex.Escape))+")(?![\\p{L}])",RegexOptions.Compiled);
        class State {public string Source,Rendered;}
        static readonly ConditionalWeakTable<Control,State> originals=new ConditionalWeakTable<Control,State>();
        public static string T(string value){if(String.IsNullOrEmpty(value))return value;string translated=English?pattern.Replace(value,m=>translations[m.Value]):value;return Regex.Replace(translated,@"\bVectorFit\b(?!\.)","TrueTrack");}
        public static void Load(){try{English=File.ReadAllText(Path.Combine(Driver.Data,"language.txt")).Trim()=="en";}catch{English=false;}}
        public static void Save(){Directory.CreateDirectory(Driver.Data);File.WriteAllText(Path.Combine(Driver.Data,"language.txt"),English?"en":"nb");}
        public static void SetText(Control control,string value){var state=originals.GetValue(control,key=>new State());state.Source=value;state.Rendered=T(value);control.Text=state.Rendered;}
        public static void Controls(Control root){
            bool previous=Applying;Applying=true;try{
            if(root is Label||root is Button||root is CheckBox||root is Form||root is GroupBox){
                var state=originals.GetValue(root,key=>new State());if(state.Source==null||root.Text!=state.Rendered)state.Source=root.Text;
                root.Text=T(state.Source);state.Rendered=root.Text;
            }
            var combo=root as ComboBox;
            if(combo!=null&&(string)combo.Tag!="language")for(int i=0;i<combo.Items.Count;i++)if(combo.Items[i] is string){string value=T((string)combo.Items[i]);if(value!=(string)combo.Items[i])combo.Items[i]=value;}
            foreach(Control child in root.Controls)Controls(child);
            root.Invalidate();
            }finally{Applying=previous;}
        }
        static Dictionary<string,string> Build(){var result=new Dictionary<string,string>();
            result["↑ høyere er bedre · ↓ lavere er bedre · * med bevart treffandel. Bevegelsesmål er medianer; avstand er % av målavstanden."]="↑ higher is better · ↓ lower is better · * with maintained hit rate. Movement measures are medians; distance is % of target distance.";
            result["kontekst"]="context";
            result["Tid per mål (inkl. bom; tidsavbrudd = 3000 ms): "]="Time per target (incl. misses; timeout = 3000 ms): ";
            // One phrase per line, separated from its English translation by a tab-like pipe delimiter.
            const string data=@"Bekreftelsesrunden er låst til samme profiltype.|The confirmation run is locked to the same profile type.
Beregn et forslag før bekreftelsesrunden.|Calculate a proposal before starting confirmation.
Samme A og B testes på nytt. Ingen nye innstillinger beregnes eller aktiveres.|The same A and B are retested. No new settings are calculated or activated.
Ugyldig kontrollhistorikk.|Invalid comparison history.
Den låste profilen er endret. Start en ny sammenligning.|The locked profile has changed. Start a new comparison.
BEKREFTELSE · SAMME A OG B|CONFIRMATION · SAME A AND B
Bekreft samme forslag på nytt|Confirm the same proposal again
Start bekreftelsesrunden|Start confirmation run
Samme forslag · klar for bekreftelsesrunde|Same proposal · ready for confirmation
Baseline klar · forslaget er ikke testet|Baseline ready · proposal not tested
Testen er ufullført · ingen konklusjon ennå|Test incomplete · no conclusion yet
Forbedringen er støttet i to påfølgende kontroller|Improvement supported in two consecutive comparisons
Denne kontrollen støtter forbedring · gjenta samme forslag|This comparison supports improvement · repeat the same proposal
Lovende tendens gjentatt · fortsatt ikke bekreftet|Promising trend repeated · still unconfirmed
Lovende utvikling · ikke bekreftet ennå|Promising progress · not yet confirmed
Nærmere klikk, men langsommere|Closer clicks, but slower
Svakere flickresultat · behold måleprofilen|Weaker flick result · keep the measured profile
Blandet eller liten forskjell · ingen klar forbedring|Mixed or small difference · no clear improvement
omtrent uendret|roughly unchanged
mindre avvik|less error
større avvik|more error
raskere|faster
langsommere|slower
Flick: A/B mangler. Kartlegging alene bekrefter ikke profileffekt.|Flick: no A/B yet. Mapping alone does not confirm a profile effect.
Presisjon: A/B mangler. Ingen konklusjon om kontroll ennå.|Precision: no A/B yet. No conclusion about control.
per profil.|per profile.
Tid til treff|Time to hit
Neste steg: test samme forslag på nytt før du legger på flere justeringer.|Next: retest the same proposal before adding further adjustments.
Samme A- og B-profil · tidligere kontroller beholdes|Same A and B profiles · previous comparisons retained
Kontroll|Comparison
ufullført|incomplete
støtter forbedring|supports improvement
lovende, ikke bekreftet|promising, unconfirmed
blandet eller svakere|mixed or weaker
Kontrollene er vurdert hver for seg. Gjentatte tester er ingen samlet statistisk garanti.|Comparisons are assessed separately. Repeated tests provide no overall statistical guarantee.
A = måleprofilen. B = samme forslag. Tallene nedenfor er A → B; tidene er medianer.|A = measured profile. B = same proposal. Values below show A → B; times are medians.
Lavere avvik er ikke nok hvis treff eller hastighet blir dårligere. Lovende beskriver tallene; bekreftet krever at kontrollene består.|Less error is not enough if hits or speed get worse. Promising describes the figures; confirmation requires passing the checks.
Kontroller av samme forslag|Comparisons of the same proposal
Innstillingene som sammenlignes|Settings being compared
Hvorfor denne vurderingen?|Why this assessment?
Resultatet gjelder dette profilparet. Ingen innstillinger endres når du leser analysen.|The result applies to this profile pair. Reading the analysis changes no settings.
For få presisjonstreff til å bekrefte kontroll. Ingen samlet forbedring er bekreftet.|Too few precision hits to establish control. No overall improvement is confirmed.
Ny analyse av samme måling. Forslaget er ikke testet.|New analysis of the same measurement. The proposal has not been tested.
Ny analyse av samme data · ikke en ny måling. Kilde:|New analysis of the same data, not a new measurement. Source:
Beregn forslag fra ellipsetendensen|Calculate proposal from ellipse tendency
Lager en ny analyse av de lagrede sirklene. Originalen beholdes. Tidligere A/B godkjenner ikke det nye forslaget. Ingen profil aktiveres.|Creates a new analysis of the saved circles. The original is retained. Earlier A/B does not approve the new proposal. No profile is activated.
Denne målingen har ikke 60 naturlige sirkelrunder.|This measurement does not have 60 natural circle laps.
MÅLING MED FASEPROFIL|MEASURING THE PHASE PROFILE
En felles ellipsetendens brukes til et forsiktig akseforslag. Vanlig variasjon reduserer styrken, men fjerner ikke tendensen.|A shared ellipse tendency informs a cautious axis proposal. Ordinary variation reduces its strength but does not discard the tendency.
Dreieretningene peker mot motsatte akseendringer. Ingen felles sirkelkorrigering foreslås.|The rotation directions suggest opposing axis changes. No shared circle correction is proposed.
Formen vises, men svært store avvik, endret tendens eller for lite formdata hindrer et felles akseforslag.|The shape is shown, but extreme variation, changing tendency or insufficient shape data prevents a shared axis proposal.
Fase 1 samler baseline og foreslår innstillinger. Velg forslaget for å starte fase 2 med korrigeringen aktiv. Hver fase har flick og presisjon, med instruksjoner og klikk for å starte hver del. Senere faser kontrollerer neste forslag.|Phase 1 records baseline and proposes settings. Choose the proposal to start phase 2 with the correction active. Each phase has flick and precision parts with instructions and a click to start each part. Later phases check the next proposal.
DEL 2 · PRESISJON|PART 2 · PRECISION
DEL 1 · FLICK|PART 1 · FLICK
ØVELSE · FLICK|PRACTICE · FLICK
A/B-KONTROLL|A/B COMPARISON
ENDRET PROFIL|CHANGED PROFILE
Treff den grønne prikken så raskt du klarer med kontroll.|Hit the green dot as quickly as you can while staying in control.
Sikt raskt mot den grønne prikken og klikk én gang.|Aim quickly at the green dot and click once.
Ved bom: korriger og klikk igjen på samme mål. Klokken fortsetter.|If you miss, adjust and click the same target again. The clock keeps running.
Første klikk går videre, også ved bom. Ikke vent for å finjustere.|The first click advances, even on a miss. Do not wait to fine-tune your aim.
Maks 3 sekunder per mål. Både tid og treff teller.|Maximum 3 seconds per target. Both time and hits count.
Klikk for å starte denne delen|Click to start this part
Bomklikk lagret. Korriger og treff samme mål.|Miss recorded. Adjust and hit the same target.
Treff lagret|Hit recorded
Tidsgrensen er nådd. Målet er lagret som mislykket.|Time limit reached. Target recorded as unsuccessful.
Ingen presisjonsmåling ennå.|No precision measurement yet.
Tid per mål|Time per target
Bomklikk|Missed clicks
Presisjonskontrollen er ikke fullført. Ingen samlet forbedring er bekreftet.|Precision comparison is incomplete. No overall improvement is confirmed.
Presisjonskontrollen viser ingen tydelig forverring innenfor grensene. Dette er ikke et eget bevis på forbedring.|Precision shows no clear regression within the limits. This is not separate evidence of improvement.
Presisjonsdelen fikk færre treff, flere bomklikk eller over 5 % lengre tid. Behold profilen.|Precision had fewer hits, more missed clicks or over 5% longer time. Keep the profile.
Presisjon · treff uten unødvendig tidsbruk|Precision · hit without unnecessary delay
Måleprofil|Measured profile
Tidsavbrudd teller som 3000 ms og mislykket mål. Alle bomklikk beholdes. Presisjon brukes som kontroll; flickbevegelsene beregner profilforslaget.|Timeouts count as 3000 ms and failed targets. All missed clicks are retained. Precision is a check; flick movements determine the proposal.
Kjør A/B · flick + presisjon|Run A/B · flick + precision
Start A/B · flick + presisjon|Start A/B · flick + precision
Presisjon|Precision
analysert|analyzed
Analysen gir første forslag.|Analysis produces the first proposal.
Behold profilen · start fase|Keep profile · start phase
Datainnhenting → analyse → neste fase|Data collection → analysis → next phase
Fase|Phase
FASE|PHASE
Fase 1 · Baseline|Phase 1 · Baseline
Analyse|Analysis
Fase 2 · Ny profil|Phase 2 · New profile
Fase 3 og videre|Phase 3 and beyond
Naturlige bevegelser.|Natural movements.
Ingen ny korreksjon.|No new correction.
Se mønsteret og konkrete|See patterns and specific
forslag til innstillinger.|proposed settings.
Bruk første korrigering.|Apply the first correction.
Samle nye målinger.|Collect new measurements.
Juster fra siste måling.|Adjust using the latest data.
Følg fart og presisjon.|Track speed and accuracy.
Fase 1 samler baseline og foreslår innstillinger. Velg forslaget for å starte fase 2 med korrigeringen aktiv. Senere faser måler, kontrollerer og foreslår neste justering. Alle klikk teller, også bom.|Phase 1 collects a baseline and proposes settings. Choose the proposal to start phase 2 with the correction active. Later phases measure, check and propose the next adjustment. Every click counts, including misses.
Baseline analysert|Baseline analyzed
Måling analysert|Measurement analyzed
Datainnhenting · ingen ferdig profil ennå|Data collection · no complete profile yet
Prøv forslaget · start fase|Try proposal · start phase
Kontrollerer forslaget|Checking the proposal
KONTROLLERER FORSLAGET|CHECKING THE PROPOSAL
BASELINE / DATAINNHENTING|BASELINE / DATA COLLECTION
DATAINNHENTING MED FASEPROFIL|DATA COLLECTION WITH PHASE PROFILE
Baseline · datainnhenting|Baseline · data collection
Datainnhenting med faseprofil|Data collection with phase profile
Analyse ferdig · velg neste fase|Analysis complete · choose next phase
Utvikling i datainnhenting · avvik og bevegelsestid|Data collection progress · error and movement time
Fra baseline|From baseline
Fra forrige fase|From previous phase
Ulik tidsbruk kan påvirke presisjonen. Dette alene bekrefter ikke profileffekt.|Different timing can affect accuracy. This alone does not confirm a profile benefit.
Naturlig bevegelse · alle runder teller|Natural movement · every lap counts
Gjør naturlige rundbevegelser rundt midten. Ikke prøv å tegne pent eller rette opp formen.|Make natural circular movements around the center. Do not try to draw neatly or correct the shape.
Gjør deg klar. Finn en behagelig bevegelse.|Get ready. Find a comfortable movement.
La formen falle naturlig. Det er ingen punkter du må treffe.|Let the shape emerge naturally. There are no points you need to hit.
Alle runder er registrert. Ingen bevegelsesdata å beregne form fra.|All laps are recorded. No movement data is available to calculate a shape.
Alle runder teller. Den naturlige formen gir et stabilt grunnlag for et prøveforslag.|Every lap counts. The natural shape provides a stable basis for a trial proposal.
Alle runder teller og formen vises. Ingen entydig sirkelkorreksjon; måltesten kan fortsatt foreslå akseendringer.|Every lap counts and the shape is shown. No clear circle correction; the target test can still propose axis changes.
Registrert|Recorded
runder|laps
Formdata · Med klokken:|Shape data · Clockwise:
Sentrert og størrelsesnormalisert. Beregnet form er en simulering; A/B må vise om siktingen blir bedre.|Centered and size-normalized. Predicted shape is simulated; A/B must confirm better aim.
Foreslåtte innstillinger|Proposed settings
Målt med|Measured with
Nytt forslag|New proposal
Endring|Change
Uendret|Unchanged
Aksemultiplikatorene kommer i tillegg til samlet følsomhet. DPI på musen endres ikke.|Axis multipliers apply in addition to overall sensitivity. Mouse DPI is unchanged.
Prøv forslaget og ta ny runde|Try proposal and start next round
Behold profilen og ta ny runde|Keep profile and start next round
A/B støtter forslaget. Innstillingene brukes først når du velger å aktivere dem.|A/B supports the proposal. Settings apply only when you choose to activate them.
Prøveforslag, ikke bekreftet bedre. Knappen lar deg prøve innstillingene globalt og måle neste runde.|Trial proposal, not confirmed better. The button lets you try the settings globally and measure the next round.
Avbryt|Cancel
Fullfør sirkeltesten først.|Complete the circle test first.
60 sirkler · valgfritt|60 circles · optional
30 med, så 30 mot klokken.|30 clockwise, then 30 counterclockwise.
Ta sirkeltest først · 30 med + 30 mot klokken (valgfritt)|Start with circles · 30 clockwise + 30 counterclockwise (optional)
Runden ble ikke fullført. Prøv samme runde igjen.|The lap was not completed. Try the same lap again.
Samme størrelse hele testen|Same size throughout the test
Medianform|Median shape
Slik ble sirklene dine|Your circle results
Med klokken:|Clockwise:
Mot klokken:|Counterclockwise:
Målt median|Measured median
Beregnet med forslag|Predicted with proposal
Ingen sirkelendring|No circle adjustment
Lengste/korteste akse:|Longest/shortest axis:
1,000 = rund · Grå stiplet linje = sirkel · Hvit = median · Blå = med · Oransje = mot|1.000 = round · Gray dashes = circle · White = median · Blue = clockwise · Orange = counterclockwise
Sentrert og skalert til samme areal. Beregnet form er en simulering; A/B må vise om siktingen blir bedre.|Centered and scaled to equal area. Predicted shape is simulated; A/B must confirm better aim.
Medianformen er stabil i begge dreieretninger.|The median shape is stable in both rotation directions.
Medianformen vises, men varierer for mye eller mangler runder. Den endrer ikke profilforslaget.|The median is shown, but varies too much or lacks laps. It does not change the proposal.
En skrå ellipse kan bare korrigeres delvis med denne profilen. Ingen global profil er endret.|A tilted ellipse can only be partly corrected by this profile. No global profile has been changed.
Medianformen bidrar til et begrenset profilforslag. A/B må bekrefte bedre sikting.|The median shape contributes to a bounded proposal. A/B must confirm better aim.
Medianformen gir ingen tydelig korreksjon innenfor profilens grenser. Følsomhet og rotasjon vurderes i måltesten.|The median yields no clear adjustment within profile limits. Sensitivity and rotation are assessed in the target test.
Fortsett til måltest|Continue to target test
Lagre og avslutt|Save and close
Aktiv profil|Active profile
Nøytraliser profil|Neutralize profile
Den aktive Raw Accel-profilen stemmer ikke med oppsettet denne målingen krever.|The active Raw Accel profile does not match the setup required by this measurement.
Nøytraliser for å fortsette målingen. Det aktive oppsettet sikkerhetskopieres først.|Neutralize to continue the measurement. Your active settings will be backed up first.
Nøytraliser for å gå til en ny måling fra nøytralt oppsett. Denne rundens A/B fortsetter ikke.|Neutralize to start a new measurement from neutral settings. This round's A/B will not continue.
Profilen stemmer fortsatt ikke med målingen. Målingen er stoppet.|The profile still does not match the measurement. Measurement stopped.
Nøytralisering kunne ikke bekreftes. Målingen er stoppet.|Neutralization could not be verified. Measurement stopped.
Følsomheten beholdes: forskjellen er for usikker i forhold til variasjonen mellom bevegelsene.|Sensitivity retained: the difference is too uncertain relative to movement variability.
32–64 forsøk totalt|32–64 attempts in total
Stopper ved tydelig resultat.|Stops when the result is clear.
Bytter dreieretning|Changing rotation direction
Bytter størrelse|Changing size
Gjør deg klar|Get ready
registrerte forsøk|recorded attempts
A/B vurderes etter 32, 48 og høyst 64 forsøk. Tydelige resultater stopper testen tidligere.|A/B is assessed after 32, 48 and at most 64 attempts. Clear results end the test early.
Forsøket er lagret, også ved bom eller tidsavbrudd.|Attempt saved, including misses and timeouts.
A/B er ikke ved et fullført kontrollpunkt ennå.|A/B has not reached a complete checkpoint yet.
A/B mangler sammenlignbare treff- eller bevegelsesdata.|A/B lacks comparable hit or movement data.
A/B mangler et komplett målpar.|A/B is missing a complete target pair.
A/B har ulik fordeling av mål. Kjør en ny test.|A/B has an unequal target distribution. Run a new test.
A/B støtter nærmere klikk uten færre treff eller tydelig dårligere kontroll.|A/B supports closer clicks without fewer hits or clearly worse control.
A/B støtter flere faktiske treff med bevart klikkpresisjon og kontroll.|A/B supports more actual hits with preserved click accuracy and control.
A/B støtter mindre etterkorrigering med minst like mange treff og bevart klikkpresisjon.|A/B supports less corrective movement with at least as many hits and preserved click accuracy.
A/B viser tydelig større klikkavvik med forslaget. Behold den aktive profilen.|A/B shows clearly larger click errors with the proposal. Keep your active profile.
A/B er ferdig, men forbedring er ikke tydelig nok. Behold den aktive profilen.|A/B is complete, but improvement is not clear enough. Keep your active profile.
Forskjellen er fortsatt usikker. A/B fortsetter med 16 forsøk til.|The difference is still uncertain. A/B continues with 16 more attempts.
Vurderingen tar hensyn til|The assessment takes into account
færre treff|fewer hits
større avvik i de svakeste forsøkene|larger errors in the worst attempts
mer etterkorrigering|more corrective movement
manglende bevegelsesbane|missing movement path
A/B inkluderer bom, klikk uten bevegelse og tidsavbrudd. Ugyldige forsøk teller som bom med minst 100 % avvik. Ukjente bevegelsesmål vises som —.|A/B includes misses, clicks without movement and timeouts. Invalid attempts count as misses with at least 100% error. Unknown movement metrics appear as —.
Treff ved klikk|Hits at click
Treff etter overskyting|Hits after overshoot
Overskyting før klikk|Overshoot before click
Etterkorrigering|Corrective movement
Tid til første målpassering|Time to first target entry
Tid etter første bevegelse|Time after initial movement
Nye passeringer av målet|Target re-entries
Treff og kontroll gjennom bevegelsen|Hits and control throughout the movement
Bevegelsesmål er medianer. Avstander er prosent av målavstanden. Treff etter retur teller fortsatt som treff.|Movement metrics are medians. Distances are percentages of target distance. Hits after returning still count as hits.
A/B vurderer klikkavstand, faktiske treff, overskyting, etterkorrigering og tid. Mindre korrigering kan gi forbedring ved bevart presisjon og minst like mange treff.|A/B assesses click distance, actual hits, overshoot, corrective movement and time. Less correction can count as improvement with preserved accuracy and at least as many hits.
Eldre måling: den opprinnelige A/B-vurderingen er beholdt. Neste runde bruker den nye vurderingen av presisjon og kontroll.|Older measurement: its original A/B assessment is preserved. The next round uses the new accuracy and control assessment.
Kjør A/B på nytt · 32–64 forsøk|Repeat A/B · 32–64 attempts
Start A/B-test · 32–64 forsøk|Start A/B · 32–64 attempts
Hjelp med museoppsettet|Mouse setup help
DPI må være lik innstillingen på musen. Synsfeltet bestemmer hvordan målene vises; bruk spillets horisontale synsfelt.|DPI must match your mouse setting. Field of view controls how targets are displayed; use your game's horizontal field of view.
Bruk i Windows og spill|Use in Windows and games
Kalibrer|Calibrate
Resultat|Results
Kartlegging|Mapping
Sirkeltest|Circle test
Sirkler|Circles
Liten|Small
Middels|Medium
Stor|Large
Med klokken|Clockwise
Mot klokken|Counterclockwise
15 sirkler · valgfritt|15 circles · optional
Tre størrelser, begge veier.|Three sizes, both directions.
8 øvelser + 48 klikk|8 warm-ups + 48 clicks
Måler både treff og bom.|Records hits and misses.
32 klikk per profil|32 clicks per profile
Nåværende mot nytt forslag.|Current vs proposed profile.
Se forskjellen|See the difference
Velg om profilen skal brukes.|Choose whether to apply it.
Bruk samme DPI, grep og musematte som i spillet. Testen finner et forslag og sammenligner det med profilen du bruker nå.|Use the same DPI, grip and mouse pad as in your game. The test builds a proposal and compares it with your current profile.
Ett klikk registrerer forsøket og går videre, også når du bommer. Målene varierer i plassering og ventetid. Hvis målingene spriker, legges 16 klikk til, opptil 80. A/B starter automatisk.|Each click records an attempt and moves on, even if you miss. Target positions and delays vary. Inconsistent measurements add 16 clicks, up to 80. A/B starts automatically.
Juster følsomheten hvis jeg ofte skyter for langt eller for kort|Adjust sensitivity when I consistently overshoot or undershoot
Ta sirkeltest først · 15 runder uten spor etter markøren (valgfritt)|Start with a circle test · 15 turns with no cursor trail (optional)
Tegn runde bevegelser gjennom de fire punktene og tilbake til det grønne. Banen vises ikke.|Draw round movements through the four points and back to the green one. Your path stays hidden.
Klikk én gang for å starte. Tegn deretter uten å klikke.|Click once to start. Then draw without clicking.
Gjør klar: følg punktene 1 → 2 → 3 → 4 → 1|Get ready: follow points 1 → 2 → 3 → 4 → 1
Følg punktene 1 → 2 → 3 → 4 → 1|Follow points 1 → 2 → 3 → 4 → 1
Sirkeltest → kartlegging → A/B → resultat|Circle test → mapping → A/B → results
Ingen komplette sirkler å sammenligne ennå.|No complete circles to compare yet.
Sirklene gir et stabilt mål på bredde og høyde.|The circles provide a consistent width and height measurement.
Sirklene varierer. De vises i statistikken, men endrer ikke profilforslaget.|The circles vary. They appear in statistics but do not change the proposed profile.
Et stabilt bredde/høyde-avvik bidrar forsiktig til forslaget. A/B må bekrefte at klikkene blir nærmere målet.|A consistent width/height deviation makes a small contribution to the proposal. A/B must confirm closer clicks.
Målingene varierer. Vi samler 16 ekstra klikk før sammenligningen.|Measurements vary. Collecting 16 more clicks before the comparison.
Trenger minst 6 målinger med bevegelse i hver av de 8 retningene.|At least 6 measurements with movement are needed in each of the 8 directions.
Avstand fra klikk til sentrum|Click distance from the center
A = profilen du målte med. B = forslaget. Tallene er medianer, i prosent av målavstanden.|A = your measured profile. B = the proposal. Values are medians, as a percentage of target distance.
Neste steg: bruk forbedringen i Windows og spill, eller fortsett kalibreringen med den aktiv.|Next: apply the improvement in Windows and games, or continue calibrating with it active.
Neste steg: behold profilen du bruker nå og ta en ny runde. Et forslag uten bekreftet forbedring bør testes mer før det tas i bruk.|Next: keep your current profile and run another round. A proposal with no confirmed improvement needs more testing before use.
Bruk forbedringen og fortsett|Apply improvement and continue
Valg for neste runde|Options for the next round
Profildetaljer og valgfri kurvekorreksjon|Profile details and optional curve correction
Den aktive profilen tilhører ikke denne målingen. Åpne målingen som hører til profilen, eller gjenopprett profilen du målte med.|The active profile does not belong to this measurement. Open the matching measurement or restore the profile you measured with.
Nøytraliser slår av Raw Accel-justeringene og lagrer en sikkerhetskopi. Gjenopprett henter tilbake oppsettet før siste endring.|Reset to neutral turns off Raw Accel adjustments and saves a backup. Restore brings back the settings from before the last change.
målbare sirkler|measurable circles
Bredde / høyde per sirkel · 1,00 = lik bredde og høyde|Width / height per circle · 1.00 = equal width and height
Bredde/høyde|Width/height
Lengste/korteste akse|Longest/shortest axis
Avstand til start|Distance from start
% av radius|% of radius
× = ufullstendig eller for ujevn til formberegning. Alle forsøk lagres.|× = incomplete or too irregular for shape fitting. All attempts are saved.
Mål bevegelsene. Test en personlig retningsprofil.|Measure your movement. Test a personal mouse profile.
Start med standard eller eget oppsett|Start with defaults or your own setup
Bruk samme DPI, grep og musematte gjennom hele testen. Standardprofilen bruker Raw Accel uten Interception. Du kan velge kurvekorreksjon separat på resultatsiden.|Use the same DPI, grip and mouse pad throughout. The default profile uses Raw Accel without Interception. Curve correction is a separate option on the results page.
Navn på mus / oppsett|Mouse / setup name
DPI på musen|Mouse DPI
Følsomhet · cm / 360°|Sensitivity · cm / 360°
Horisontalt synsfelt|Horizontal field of view
Slik finner du cm/360°: Mål hvor langt du flytter musen på musematten for én hel omdreining i spillet. Standardverdien 40 er et utgangspunkt, ikke en avlesning fra G703.|To find cm/360°: measure how far you move your mouse for one full turn in your game. The default of 40 is a starting point, not a reading from your G703.
8 øvelsesflick → 80 målinger → 64 A/B-flick automatisk|8 warm-up flicks → 80 measurements → 64 automatic A/B flicks
Ett startklikk. Fortsett å sikte og klikke i samme vindu.|Click once to start. Keep aiming and clicking in the same window.
Bomskudd og hele bevegelsen registreres. Nye mål kommer automatisk.|Misses and the full movement path are recorded. New targets appear automatically.
A/B sammenligner måleprofilen med det nye forslaget. Esc avslutter. Alt+Tab setter på pause.|A/B compares your current profile with the proposal. Esc exits. Alt+Tab pauses.
Juster også følsomhet ved over-/underskyting i neste runde|Also adjust sensitivity for overshooting / undershooting in the next round
Start kalibrering|Start calibration
Åpne lagret måling|Open saved measurement
Åpne en annen måling|Open another measurement
Åpne en lagret måling for å se baseline, A/B og tidligere runder.|Open a saved measurement to see its baseline, A/B and earlier rounds.
Ingen ferdig profil ennå|No completed profile yet
Din beregnede profil|Your calculated profile
Kalibrer musen først. Her ser du målingene og kan teste korreksjonen før den brukes utenfor programmet.|Calibrate your mouse first. Review measurements and test the correction here before applying it globally.
Gå til kalibrering|Go to calibration
Profiltype|Profile type
Standard · Raw Accel (uten Interception)|Default · Raw Accel (without Interception)
Valgfritt · Interception + kurvekorreksjon|Optional · Interception + curve correction
A/B tester følsomhet, retningsprofil og kurvekorreksjon. Global bruk krever Interception og at VectorFit kjører.|A/B tests sensitivity, direction and curve correction. Global use requires Interception and a running VectorFit app.
A/B tester følsomhet, retning og rotasjon slik Raw Accel bruker dem. Kurver og bomskudd måles fortsatt. Ingen Interception kreves.|A/B tests sensitivity, direction and rotation as applied by Raw Accel. Curves and misses are still recorded. Interception is not required.
A: profilen du målte med|A: your measured profile
B: nytt forslag|B: new proposal
Kjør A/B på nytt · 64 flick|Repeat A/B · 64 flicks
Start A/B-test · 64 flick|Start A/B test · 64 flicks
Grafer og historikk|Charts and history
Vis lagrede målinger|Show saved measurements
Bruk forslaget og ta ny runde|Use proposal and start next round
Behold måleprofilen og ta ny runde|Keep measured profile and start next round
Målinger med bevegelse|Measurements with movement
Samlet følsomhet|Overall sensitivity
Retning maks ±8 %, rotasjon ±3°.|Direction: max ±8%, rotation: ±3°.
Følsomhet: maks 5 % per runde, totalt 0,75–1,25×.|Sensitivity: max 5% per round, total 0.75–1.25×.
Før retningsjustering. DPI på musen endres ikke.|Before directional adjustment. Mouse DPI is unchanged.
Denne runden er målt med forrige profil aktiv. A/B sammenligner den med det nye forslaget. Forrige måling er lagret som|This round was measured with the previous profile active. A/B compares it with the proposal. Previous measurement:
Hver profiltype har sin egen A/B-test. A/B-korreksjonen skjer bare inne i testvinduet. Etter aktivering kan du starte neste runde fra Global profil.|Each profile type has its own A/B test. Test corrections only affect the test window. After activation, start the next round from Global profile.
Bruk profilen globalt|Apply the profile globally
Åpne en måling eller kalibrer først.|Open a measurement or calibrate first.
A/B-testen bekrefter forbedring for denne profiltypen.|A/B confirms improvement for this profile type.
A/B-testen bekrefter forbedring.|A/B confirms improvement.
Forbedring er ikke bekreftet. Du kan velge å prøve profilen ved å trykke Aktiver med Raw Accel.|Improvement is not confirmed. You can choose to try the profile with Activate with Raw Accel.
Følsomhet, retning og rotasjon brukes av Raw Accel på kjernenivå, også med rå museinput i spill. VectorFit kan lukkes. Interception brukes ikke.|Raw Accel applies sensitivity, direction and rotation at kernel level, including raw mouse input in games. You can close VectorFit. Interception is not used.
Valgfri, eksperimentell korreksjon av retning og buer. Krever en separat Interception-driver og at VectorFit fortsetter å kjøre. Stopp med Ctrl+Alt+F12.|Optional, experimental direction and curve correction. Requires a separate Interception driver and a running VectorFit app. Stop with Ctrl+Alt+F12.
Aktiver med Raw Accel|Activate with Raw Accel
Åpne Interception-valg|Open Interception options
Eksporter Raw Accel-profil|Export Raw Accel profile
Ny runde med aktiv profil|Next round with active profile
Raw Accel kunne ikke leses.|Could not read Raw Accel.
Raw Accel er tilgjengelig og nøytral.|Raw Accel is available and neutral.
Raw Accel har en aktiv profil.|Raw Accel has an active profile.
Raw Accel: nøytral|Raw Accel: neutral
Raw Accel: profil aktiv|Raw Accel: profile active
Nøytraliser før måling|Reset to neutral before measuring
Gjenopprett profil|Restore profile
Oppdater status|Refresh status
Anti-cheat: FACEIT blokkerer Interception-driveren. Å stoppe VectorFit avinstallerer ikke driveren. Bruk bare der driveren er tillatt. Ingen driver installeres automatisk.|Anti-cheat: FACEIT blocks the Interception driver. Stopping VectorFit does not uninstall it. Use only where permitted. No driver is installed automatically.
Denne profilen endrer ikke bevegelsesbuene dynamisk. Slik kurvekorreksjon kan velges separat og krever en egen A/B-test. Ingen generell garanti for godkjenning av anti-cheat.|This profile does not dynamically alter curved movements. Curve correction is a separate option with its own A/B test. Anti-cheat approval is not guaranteed.
Stopp den globale profilen før kalibrering eller A/B-test, slik at input ikke korrigeres to ganger.|Stop the global filter before calibration or A/B to avoid applying correction twice.
Fyll ut museoppsettet først.|Complete your mouse setup first.
Starte en ny måling fra nøytralt oppsett? Bruk Ny runde med aktiv profil på Global profil for å bygge videre.|Start a new measurement from neutral settings? To continue refining, use Next round with active profile on the Global profile page.
Ny måling|New measurement
Fullfør kalibreringen slik at en profil kan beregnes først.|Complete calibration to calculate a profile first.
Driverstatus er usikker. Kalibrering er stoppet for å unngå dobbel korreksjon.|Driver status is uncertain. Calibration has stopped to avoid applying correction twice.
Profilen du målte med er ikke aktiv lenger. Bruk Gjenopprett profil før du fortsetter med den.|The profile you measured with is no longer active. Restore it before continuing.
Aktiver den valgte Raw Accel-profilen først. Neste runde må måles med akkurat denne profilen, slik at korreksjonen ikke telles to ganger.|Activate the selected Raw Accel profile first. The next round needs this exact profile to account for the existing correction.
Denne runden må testes med profilen den ble målt med. Gjenopprett forrige profil før A/B-test, eller start en ny runde med den aktive profilen.|Test this round with the profile used to measure it. Restore that profile before A/B, or start a new round with the active profile.
En annen Raw Accel-profil er aktiv. Bruk Ny runde med aktiv profil for å bygge videre, eller Nøytraliser før en ny måling fra bunnen.|Another Raw Accel profile is active. Use Next round with active profile to continue, or reset to neutral for a fresh calibration.
Driveroppsettet ble endret under testen. Målingen brukes ikke. Kalibrer på nytt.|Driver settings changed during the test. This measurement will not be used. Calibrate again.
Målefilen er for stor.|The measurement file is too large.
Stopp Interception-profilen før du bytter profil eller endrer Raw Accel.|Stop the Interception filter before switching profiles or changing Raw Accel.
Velg Interception-profilen først.|Select the Interception profile first.
Velg Standard · Raw Accel først.|Select Default · Raw Accel first.
Åpne den lagrede målingen din, eller fullfør kalibreringen først.|Open your saved measurement or complete calibration first.
Kalibreringen er ikke komplett, eller profilen ligger utenfor tillatte grenser.|Calibration is incomplete or the profile exceeds the allowed limits.
Profilen er ikke aktivert.|The profile has not been activated.
Forbedringen er ikke bekreftet.|Improvement is not confirmed.
Forbedring er ikke bekreftet.|Improvement is not confirmed.
Profilen gjelder alle mus. Nåværende Raw Accel-oppsett sikkerhetskopieres først. Du kan angre med Gjenopprett profil.|The profile applies to all mice. Your current Raw Accel settings will be backed up first. You can undo this with Restore profile.
Aktivere profilen?|Activate this profile?
Vil du likevel prøve profilen?|Try the profile anyway?
Aktiver retningsprofil|Activate mouse profile
Profilen er aktiv. Innstillingene er lest tilbake fra driveren og kontrollert.|The profile is active. Settings have been read back from the driver and verified.
Eksportere en prøveprofil likevel?|Export a trial profile anyway?
Eksporter prøveprofil|Export trial profile
Raw Accel-profilen er lagret her:|Raw Accel profile saved here:
Den inneholder retningsfølsomhet og rotasjon. Ingen innstillinger er aktivert.|It contains sensitivity, direction and rotation. No settings have been activated.
Den inneholder følsomhet, retning og rotasjon. Ingen innstillinger er aktivert.|It contains sensitivity, direction and rotation. No settings have been activated.
Profil eksportert|Profile exported
Ingen tilgjengelig driver å nøytralisere.|No available driver to reset to neutral.
Oppsettet er allerede nøytralt.|Settings are already neutral.
Ingen sikkerhetskopi er laget av VectorFit ennå.|VectorFit has not created a backup yet.
Forrige oppsett er gjenopprettet og kontrollert.|Previous settings restored and verified.
Klikkposisjon / målavstand. Ringen er 100 %.|Click position / target distance. Ring = 100%.
Baseline er målingen før A/B. A er profilen du målte med; B er forslaget. Negative differanser betyr lavere avvik eller kortere tid. Avvik vises som prosent av målavstanden.|Baseline is measured before A/B. A is your current profile; B is the proposal. Negative differences mean less error or faster clicks. Error is a percentage of target distance.
sammenhengende runder · Endring i baseline|linked rounds · Baseline change
A/B i samme runde har like målvinkler og avstander. Mellom runder varierer målene; trendene er veiledende.|A/B within a round uses matching target angles and distances. Targets vary across rounds; trends are indicative.
Treffandel mangler i eldre målinger. Nye runder lagrer treff på den grønne prikken; bomskudd er fortsatt målinger.|Older measurements have no hit rate. New rounds record hits on the green dot; misses still count as measurements.
Treff i baseline|Baseline hits
pp = prosentpoeng. Tomme resultater vises som —. Delvise A/B-tester er foreløpige; se vurderingen under Resultat og A/B.|pp = percentage points. Missing results show —. Partial A/B tests are provisional; see the assessment under Results and A/B.
Blå: baseline per runde   ·   Grønn: B-forslag   ·   Stiplet: første baseline   ·   Lavere er bedre|Blue: round baseline   ·   Green: B proposal   ·   Dashed: first baseline   ·   Lower is better
Klikkavvik gjennom rundene|Click error across rounds
Tid til klikk gjennom rundene|Time to click across rounds
Historikken har en ugyldig lenke.|History contains an invalid link.
En tidligere målefil mangler eller er for stor.|An earlier measurement file is missing or too large.
Historikken stopper ved et annet oppsett eller brudd i rundene.|History stops at a different setup or a break in the round sequence.
En tidligere målefil kunne ikke leses.|Could not read an earlier measurement file.
Ikke nok data|Not enough data
Forbedring bekreftet|Improvement confirmed
Vurder resultatet|Review the result
Klar for A/B|Ready for A/B
Klikk for å starte|Click to start
Klikk når du er klar|Click when ready
Klikk én gang for å starte. Deretter ett klikk per mål, treff eller bom.|Click once to start. Then one click per target, hit or miss.
MÅLEPROFIL MOT NYTT FORSLAG|MEASURED PROFILE VS NEW PROPOSAL
Esc avslutter  ·  Alt+Tab setter på pause|Esc exits  ·  Alt+Tab pauses
Øvelsesrunde: lagres ikke|Warm-up: not recorded
registrerte klikk|recorded clicks
Måledelen er ferdig. Nå sammenlignes måleprofilen med det nye forslaget. Fortsett å sikte og klikke.|Measurement complete. Now comparing your profile with the new proposal. Keep aiming and clicking.
Kunne ikke lese musen|Could not read the mouse
Pause. Trykk mellomrom for å fortsette.|Paused. Press Space to continue.
Bruk en vanlig mus med relativ bevegelse.|Use a regular mouse with relative movement.
Bruk samme mus og tilkobling som i kalibreringen.|Use the same mouse and connection as during calibration.
Tiden gikk ut|Timed out
Øvelse. Flick og klikk naturlig, også når du bommer.|Warm-up. Flick and click naturally, including misses.
Klikk og bevegelse lagret|Click and movement saved
Forsøket gjentas.|The trial will be repeated.
av målavstanden|of target distance
Beregn en standardprofil før du starter neste runde.|Calculate a default profile before starting the next round.
Denne målingen støttes ikke. Start en ny kalibrering.|This measurement is not supported. Start a new calibration.
Ugyldig målefil.|Invalid measurement file.
Ukjent profiltype.|Unknown profile type.
Mangler bevegelsesdata|Missing movement data
Ugyldige bevegelsesdata|Invalid movement data
Klikk uten bevegelse: lagret, men gir ingen retningsmåling|Click without movement: saved, but no directional measurement
Forrige profil ligger utenfor tillatte grenser.|The previous profile exceeds the allowed limits.
Trenger minst 8 målinger med bevegelse i hver av de 8 retningene.|Need at least 8 measurements with movement in each of the 8 directions.
Gjentatt overskyting|Repeated overshooting
Gjentatt underskyting|Repeated undershooting
følsomheten foreslås fra|proposed sensitivity changes from
til|to
A/B avgjør om det hjelper.|A/B evaluates whether it helps.
Følsomheten beholdes: ingen stabil over- eller underskyting i minst 6 av 8 retninger.|Sensitivity unchanged: no consistent overshooting or undershooting in at least 6 of 8 directions.
Samlet følsomhet er beholdt for denne målingen.|Overall sensitivity was preserved for this measurement.
Profilen er beregnet. A/B-testen kan sammenligne forslaget med oppsettet du målte med.|Profile calculated. A/B can compare the proposal with your measured setup.
Målingene varierer. Profilen kan prøves, men effekten er usikker. A/B-test anbefales.|Measurements vary. You can try the profile, but its effect is uncertain. An A/B test is recommended.
A/B-testen er ikke fullført.|A/B test incomplete.
A/B-testen har ulike målavstander. Kjør en ny, komplett test.|A/B target distances do not match. Run a new complete test.
A/B-testen har ulike målvinkler. Kjør en ny, komplett test.|A/B target angles do not match. Run a new complete test.
A/B-testen for|The A/B test for
viser forbedring uten tydelig tap i tid eller treff.|shows improvement without a clear loss in speed or accuracy.
kalibreringen varierer|calibration varies
ingen tydelig forbedring i retning|no clear directional improvement
eller bøy|or curve improvement
langsommere klikk|slower clicks
større klikkavvik|greater click error
A/B har ikke bekreftet forbedring|A/B has not confirmed improvement
A/B viser minst 5 % mindre medianavstand til målets sentrum, uten tydelig tap i tid eller retning.|A/B shows at least 5% lower median distance to the target center, without a clear loss in speed or direction.
klikkene er ikke minst 5 % nærmere målets sentrum|clicks are not at least 5% closer to the target center
større retningsspredning|greater directional spread
Målet er mindre avstand til sentrum ved klikk. Både treff og bom teller. A/B krever minst 5 % mindre medianavstand for bekreftet forbedring.|The goal is to click closer to the center. Hits and misses both count. A/B requires at least 5% lower median distance to confirm improvement.
Eldre måling: den opprinnelige A/B-vurderingen er beholdt. Neste runde krever at klikkene kommer nærmere målets sentrum.|Older measurement: the original A/B assessment is preserved. The next round will require clicks to be closer to the target center.
Raw Accel uten Interception|Raw Accel without Interception
Interception + kurvekorreksjon|Interception + curve correction
Valgfri Interception-profil. Last inn en testet kurveprofil og velg musen.|Optional Interception profile. Load a tested curve profile and select your mouse.
Valgfri Interception-profil|Optional Interception profile
Aktiver globalt|Activate globally
Profilen gjelder den valgte musen i Windows og alle programmer.|The profile applies to the selected mouse in Windows and all applications.
Stopp med Ctrl+Alt+F12 eller Stopp-knappen. Raw Accel må være nøytral.|Stop with Ctrl+Alt+F12 or the Stop button. Raw Accel must be neutral.
Eksperimentell kurvekorreksjon. Krever Interception-driveren og omstart.|Experimental curve correction. Requires the Interception driver and a restart.
FACEIT blokkerer Interception-driveren. Å stoppe filteret avinstallerer ikke driveren.|FACEIT blocks the Interception driver. Stopping the filter does not uninstall it.
Bruk bare der Interception er tillatt. Dette vinduet installerer ingen driver.|Use only where Interception is permitted. This window does not install any driver.
Åpne måling|Open measurement
Finn mus|Find mouse
Stopper …|Stopping …
Stopp korreksjonen før du endrer oppsettet.|Stop correction before changing settings.
Åpne målingen i VectorFit, velg Interception og fullfør A/B-testen for denne profiltypen først.|Open the measurement in VectorFit, select Interception and complete its A/B test first.
Måling lastet|Measurement loaded
Kurveområder|Curve regions
Målingen er ikke klar for Interception. Velg Interception-profilen og fullfør dens A/B-test som bekrefter forbedring.|Measurement is not ready for Interception. Select the Interception profile and complete an A/B test that confirms improvement.
Valgt måling|Selected measurement
Velg musen med Finn mus.|Select your mouse using Find mouse.
Interception-driveren er ikke tilgjengelig. Kurvekorreksjonen kan ikke aktiveres før driveren er tilgjengelig.|The Interception driver is unavailable. Curve correction cannot be activated until it is available.
Den forrige kjøringen avsluttes fortsatt.|The previous run is still stopping.
Velg måling og mus først.|Select a measurement and a mouse first.
Profiltypen er endret eller Interception-testen er ikke bestått. Velg en testet Interception-profil.|Profile type changed or Interception A/B was not passed. Select a tested Interception profile.
Raw Accel må være tilgjengelig og nøytral, eller ikke installert. Åpne VectorFit for å nøytralisere profilen.|Raw Accel must be available and neutral, or not installed. Open VectorFit to reset it to neutral.
Aktiverer profilen globalt. Ctrl+Alt+F12 stopper.|Activating the profile globally. Ctrl+Alt+F12 stops it.
Korreksjonen er stoppet.|Correction stopped.
Kunne ikke åpne Interception-driveren.|Could not open the Interception driver.
Musen er endret. Finn og velg musen på nytt.|The mouse changed. Find and select it again.
Musefilteret kunne ikke aktiveres.|Could not activate the mouse filter.
Aktiv globalt: den valgte musen korrigeres i Windows og spill. Ctrl+Alt+F12 stopper.|Active globally: the selected mouse is corrected in Windows and games. Ctrl+Alt+F12 stops it.
Korreksjonen stoppet ved en beregningsfeil.|Correction stopped due to a calculation error.
Driveren kunne ikke sende museinput videre. Filteret stoppes.|The driver could not forward mouse input. Stopping the filter.
Valgfritt Interception-bibliotek mangler. Raw Accel-profilen fungerer uten dette.|Optional Interception library missing. The Raw Accel profile works without it.
Driveren svarte ikke innen 12 sekunder.|The driver did not respond within 12 seconds.
Profilen ligger utenfor tillatte grenser.|The profile exceeds the allowed limits.
Profilen må være beregnet og innenfor grensene. En profil uten bekreftet forbedring krever et uttrykkelig valg før eksport eller aktivering.|The profile must be calculated and within limits. A profile without confirmed improvement requires an explicit choice before export or activation.
Ugyldig sikkerhetskopi.|Invalid backup.
Gjenoppretting feilet også|Restore also failed
Sikkerhetskopi|Backup
Tidligere oppsett er gjenopprettet.|Previous settings restored.
Ugyldig kurveprofil|Invalid curve profile
Ukjent mus|Unknown mouse
VectorFit kunne ikke starte.|VectorFit could not start.
Pakk ut hele mappen før du åpner TrueTrack.exe.|Extract the entire folder before opening TrueTrack.exe.
Driver ikke tilgjengelig|Driver unavailable
Kalibrering og global profil|Calibration and global profile
Kalibrering og A/B|Calibration and A/B
Resultat og A/B|Results and A/B
Global profil|Global profile
KALIBRERING|CALIBRATION
Kalibrering|Calibration
Statistikk|Statistics
RUNDE|ROUND
Runde|Round
runde|round
Øvelse|Warm-up
Måling|Measurement
Retningsspredning|Directional spread
Klikkavvik|Click error
klikkavvik|click error
Tid til klikk|Time to click
Lavere er bedre|Lower is better
prosentpoeng|percentage points
klikk|clicks
Høyre|Right
Venstre|Left
Ned-høyre|Down-right
Ned-venstre|Down-left
Opp-høyre|Up-right
Opp-venstre|Up-left
Ned|Down
Opp|Up
Rotasjon|Rotation
Avvik|Error
Stopp|Stop
Mus|Mouse
VectorFit-målinger|VectorFit measurements
VectorFit-måling|VectorFit measurement";
            foreach(string line in data.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){int split=line.IndexOf('|');result.Add(line.Substring(0,split),line.Substring(split+1));}return result;
        }
    }
    static class UiMessage {
        public static DialogResult Show(IWin32Window owner,string text,string caption="VectorFit",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1){return MessageBox.Show(owner,L.T(text),L.T(caption),buttons,icon,defaultButton);}
        public static DialogResult Show(string text,string caption="VectorFit"){return MessageBox.Show(L.T(text),L.T(caption));}
    }
    static class PaintText {
        public static void Draw(Graphics g,string text,Font font,Brush brush,float x,float y){g.DrawString(L.T(text),font,brush,x,y);}
        public static void Draw(Graphics g,string text,Font font,Brush brush,RectangleF rectangle){g.DrawString(L.T(text),font,brush,rectangle);}
    }
}
