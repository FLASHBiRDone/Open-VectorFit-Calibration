using System;
using System.Linq;
using System.Collections.Generic;
namespace VectorFit {
 public static class ProfileEvidence {
  public static double[] Interval(double[] differences){if(differences.Length<8||differences.Any(v=>Double.IsNaN(v)||Double.IsInfinity(v)))return null;var random=new Random(7193);var values=new double[2000];for(int b=0;b<values.Length;b++){double sum=0;for(int i=0;i<differences.Length;i++)sum+=differences[random.Next(differences.Length)];values[b]=sum/differences.Length;}Array.Sort(values);return new[]{values[49],values[1949]};}
  public static double[][] Bounds(Session s){string why;if(!PrecisionCheck.Acceptable(s.PrecisionValidation,out why,true))return new double[2][];
   var pairs=s.PrecisionValidation.GroupBy(t=>t.PairId).ToList();var a=pairs.Select(g=>g.Single(t=>!t.Corrected)).ToList();var b=pairs.Select(g=>g.Single(t=>t.Corrected)).ToList();
   var times=Interval(a.Select((t,i)=>PrecisionCheck.Time(t)-PrecisionCheck.Time(b[i])).ToArray());
   var qa=a.Select(Trajectory.Measure).ToList();var qb=b.Select(Trajectory.Measure).ToList();double[] control=null;if(qa.Concat(qb).All(q=>q.Available))control=Interval(qa.Select((q,i)=>100*(q.CorrectionDistance-qb[i].CorrectionDistance)).ToArray());return new[]{times,control};
  }
  public static bool Supported(Session s){var bounds=Bounds(s);return bounds.Any(b=>b!=null&&b[0]>0);}
  public static int Repeats(Session s){string key=ResultStory.Key(s);return s.PriorComparisons.Count(r=>r.Key==key&&r.Complete)+(ResultStory.Record(s).Complete?1:0);}
  public static bool RobustConfirmed(Session s){var record=ResultStory.Record(s);var records=s.PriorComparisons.Where(r=>r.Key==record.Key&&r.Complete).Concat(record.Complete?new[]{record}:new ComparisonRecord[0]).ToList();return s.FrozenProposal&&records.Count>=3&&records.Skip(records.Count-3).All(r=>r.Passed&&r.EvidenceSupported);}
  public static string Summary(Session s){var bounds=Bounds(s);Func<double[],string,string> line=(b,unit)=>b==null?Practice.Text("Mangler tilstrekkelige pardata","Insufficient paired data"):b[0].ToString("F1")+" … "+b[1].ToString("F1")+" "+unit;
   return Practice.Text("Skjult A/B: samme mål i balansert rekkefølge. ","Blind A/B: matched targets in a balanced order. ")+Repeats(s)+Practice.Text(" fullførte sammenligninger av samme forslag."," completed comparisons of this proposal.")+"\n"+Practice.Text("Anslått tidsgevinst, 95 % bootstrap-intervall: ","Estimated time saved, 95% bootstrap interval: ")+line(bounds[0],"ms")+"\n"+Practice.Text("Mindre etterkorrigering, intervall: ","Less corrective travel, interval: ")+line(bounds[1],Practice.Text("prosentpoeng","percentage points"))+"\n"+Practice.Text("Positivt er bedre. Et intervall som krysser null er usikkert. Tre kontroller krever tre påfølgende godkjente resultater med støttende intervall. Ta gjerne siste kontroll en annen dag. Øvelse og dagsform kan fortsatt påvirke resultatet.","Positive is better. An interval crossing zero is uncertain. Three-check confirmation requires three consecutive passing results with a supporting interval. Consider doing the last check on another day. Practice and daily variation can still affect results.");
  }
 }
}
