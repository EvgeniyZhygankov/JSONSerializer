using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Windows;

namespace JSONSerializer
{
    class Root
    {
        public List<JSON_FSSs> FSSs { get; set; }

        [JsonProperty("sites")]
        public List<JSON_site> Sites { get; set; }
    }

    class JSON_FSSs : IEqualityComparer<JSON_FSSs>
    {
        [JsonProperty("domains")]
        public string[] Domains { get; set; }

        [JsonProperty("validation")]
        public Validation Validation { get; set; }

        public bool Equals(JSON_FSSs obj1, JSON_FSSs obj2)
        {
            try
            {
                if (obj1 == null || obj2 == null) return false;

                if ((obj1.Validation == null && obj2.Validation != null) ||
                    (obj1.Validation != null && obj2.Validation == null))
                    return false;

                if (obj1.Validation == null && obj2.Validation == null)
                    return ReferenceEquals(obj1, obj2) ||
                        Enumerable.SequenceEqual(obj1.Domains, obj2.Domains);
                else
                    return ReferenceEquals(obj1, obj2) ||
                        Enumerable.SequenceEqual(obj1.Domains, obj2.Domains) &&
                        obj1.Validation.Equals(obj2.Validation);
            }
            catch (Exception ex)
            {
                MainWindow.ShowException(ex, "Domains = " + string.Join(", ", obj1.Domains));
                return false;
            }
        }

        public int GetHashCode(JSON_FSSs obj)
        {
            int res = 0;
            foreach (var item in obj.Domains)
            {
                for (int i = 0; i < item.Length; i++)
                {
                    res += item[i];
                }
            }

            return res;
        }

    }

    class JSON_site : IEqualityComparer<JSON_site>
    {
        public bool FSS_search_enabled { get; set; }
        public Interval FSS_search_interval { get; set; }

        [JsonProperty("domains")]
        public string[] Domains { get; set; }

        [JsonProperty("validation_interval")]
        public Interval Validation_interval { get; set; }

        [JsonProperty("phrase_search")]
        public Phrase_search Phrase_search { get; set; }

        public bool Equals(JSON_site obj1, JSON_site obj2)
        {
            try
            {
                if (obj1 == null || obj2 == null) return false;

                if (ReferenceEquals(obj1, obj2)) return true;

                if ((obj1.Phrase_search == null && obj2.Phrase_search != null) ||
                    (obj1.Phrase_search != null && obj2.Phrase_search == null))
                    return false;

                if (Enumerable.SequenceEqual(obj1.Domains, obj2.Domains) && 
                    obj1.FSS_search_enabled == obj2.FSS_search_enabled &&
                    obj1.FSS_search_interval.Equals(obj2.FSS_search_interval) &&
                    obj1.Validation_interval.Equals(obj2.Validation_interval))
                {
                    if (obj1.Phrase_search == null && obj2.Phrase_search == null)
                    {
                        return true;
                    }
                    else
                        return obj1.Phrase_search.Equals(obj2.Phrase_search);
                }
                else
                    return false;

                //if (obj1.Phrase_search == null && obj2.Phrase_search == null)
                //    return ReferenceEquals(obj1, obj2) ||
                //        Enumerable.SequenceEqual(obj1.Domains, obj2.Domains) &&
                //        obj1.FSS_search_enabled == obj2.FSS_search_enabled &&
                //        obj1.FSS_search_interval.Equals(obj2.FSS_search_interval) &&
                //        obj1.Validation_interval.Equals(obj2.Validation_interval);
                //else
                //    return ReferenceEquals(obj1, obj2) || 
                //        Enumerable.SequenceEqual(obj1.Domains, obj2.Domains) &&
                //        obj1.FSS_search_enabled == obj2.FSS_search_enabled &&
                //        obj1.FSS_search_interval.Equals(obj2.FSS_search_interval) &&
                //        obj1.Validation_interval.Equals(obj2.Validation_interval) &&
                //        obj1.Phrase_search.Equals(obj2.Phrase_search);
            }
            catch(Exception ex)
            {
                MainWindow.ShowException(ex, "Domains = " + string.Join(", ", obj1.Domains));
                return false;
            }
            
        }

        public int GetHashCode(JSON_site obj)
        {
            int res = 0;
            foreach (var item in obj.Domains)
            {
                for (int i = 0; i < item.Length; i++)
                {
                    res += item[i];
                }
            }

            return res;
        }
    }

    class Validation
    {
        [JsonProperty("must_include")]
        public string[] Must_include { get; set; }

        [JsonProperty("must_include_one_of")]
        public string[] Must_include_one_of { get; set; }

        [JsonProperty("must_not_include")]
        public string[] Must_not_include { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is Validation x))
                return false;
            else
                x = (Validation)obj;

            return (ReferenceEquals(this, x)) || 
                Enumerable.SequenceEqual(this.Must_include, x.Must_include) &&
                Enumerable.SequenceEqual(this.Must_include_one_of, x.Must_include_one_of) &&
                Enumerable.SequenceEqual(this.Must_not_include, x.Must_not_include);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    class Phrase_search : Validation
    {
        [JsonProperty("interval")]
        public Interval Interval { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is Phrase_search x))
                return false;
            else
                x = (Phrase_search)obj;


            return ReferenceEquals(this, x) || 
                Enumerable.SequenceEqual(this.Must_include, x.Must_include) &&
                Enumerable.SequenceEqual(this.Must_include_one_of, x.Must_include_one_of) &&
                Enumerable.SequenceEqual(this.Must_not_include, x.Must_not_include) && 
                this.Interval.Equals(x.Interval);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    class Interval
    {
        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        public Interval(string From = "", string To = "")
        {
            this.From = From;
            this.To = To;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Interval x))
                return false;
            else
                x = (Interval)obj;

            if (ReferenceEquals(this, x)) return true;

            return this.From == x.From && this.To == x.To;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
//    class JSON_site
//    {
//        public Interval FSS_search_interval { get; set; }

//        [JsonProperty("domains")]
//        public string[] Domains { get; set; }
//        public Interval Validation_interval { get; set; }
//        public Phrase_search Phrase_search { get; set; }
//    }

//    class JSON_FSSs
//    {
//        [JsonProperty("domains")]
//        public string[] Domains { get; set; }

//        [JsonProperty("validation")]
//        public Phrase_search Validation { get; set; }
//    }

//    class Root
//    {
//        public JSON_site[] FSSs { get; set; }

//        [JsonProperty("sites")]
//        public JSON_site[] Sites { get; set; }
//    }

//    class Phrase_search : Interval
//    {
//        public string[] Must_include { get; set; }
//        public string[] Must_include_one_of { get; set; }
//        public string[] Must_not_include { get; set; }

//        public Phrase_search(bool On = false, string From = "", string To = "", 
//            string[] Must_include = null, string[] Must_include_one_of = null, string[] Must_not_include = null) 
//            : base(On, From, To)
//        {
//            if (this.On)
//            {
//                this.Must_include = Must_include;
//                this.Must_include_one_of = Must_include_one_of;
//                this.Must_not_include = Must_not_include;
//            }
//        }
//    }

//    class Interval
//    {
//        public bool On { get; set; }
//        public string From { get; set; }
//        public string To { get; set; }

//        public Interval(bool On = false, string From = "", string To = "")
//        {
//            this.On = On;
//            if (On)
//            {
//                this.From = From;
//                this.To = To;
//            }
//        }
//    }
//}
