using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using FTAnalyzer.Utilities;
using System.Text.RegularExpressions;
using FTAnalyzer.Properties;

namespace FTAnalyzer
{
    public class Family : IDisplayFamily
    {
        public static readonly string UNKNOWN = "Unknown", SOLOINDIVIDUAL = "Solo", PRE_MARRIAGE = "Pre-Marriage";
        public static readonly string SINGLE = "Single", MARRIED = "Married", UNMARRIED = "Unmarried";

        public string FamilyID { get; private set; }
        public IList<Fact> Facts { get; private set; }
        public List<Individual> Children { get; internal set; }
        public Individual Husband { get; internal set; }
        public Individual Wife { get; internal set; }
        public int ExpectedTotal { get; internal set; }
        public int ExpectedAlive { get; internal set; }
        public int ExpectedDead { get; internal set; }
        public string FamilyType { get; internal set; }

        private Dictionary<string, Fact> preferredFacts;

        private Family(string familyID)
        {
            FamilyID = familyID;
            Facts = new List<Fact>();
            Children = new List<Individual>();
            preferredFacts = new Dictionary<string, Fact>();
            ExpectedTotal = 0;
            ExpectedAlive = 0;
            ExpectedDead = 0;
            FamilyType = UNKNOWN;
            if (familyID.Length>2)
            {
                if (familyID.Substring(0, 2).Equals("SF"))
                    FamilyType = SOLOINDIVIDUAL;
                else if (familyID.Substring(0, 2).Equals("PM"))
                    FamilyType = PRE_MARRIAGE;
            }
        }

        public Family() : this(string.Empty) { }

        public Family(XmlNode node, IProgress<string> outputText)
            : this(string.Empty)
        {
            if (node != null)
            {
                XmlNode eHusband = node.SelectSingleNode("HUSB");
                XmlNode eWife = node.SelectSingleNode("WIFE");
                FamilyID = node.Attributes["ID"].Value;
                string husbandID = eHusband == null || eHusband.Attributes["REF"] == null ? null : eHusband.Attributes["REF"].Value;
                string wifeID = eWife == null || eWife.Attributes["REF"] == null ? null : eWife.Attributes["REF"].Value;
                FamilyTree ft = FamilyTree.Instance;
                Husband = ft.GetIndividual(husbandID);
                Wife = ft.GetIndividual(wifeID);
                if (Husband != null && Wife != null)
                    Wife.MarriedName = Husband.Surname;
                if(Husband !=null)
                    Husband.FamiliesAsParent.Add(this);
                if(Wife != null)
                    Wife.FamiliesAsParent.Add(this);
                // now iterate through child elements of eChildren
                // finding all individuals
                XmlNodeList list = node.SelectNodes("CHIL");
                foreach (XmlNode n in list)
                {
                    if (n.Attributes["REF"] != null)
                    {
                        Individual child = ft.GetIndividual(n.Attributes["REF"].Value);
                        if (child != null)
                        {
                            XmlNode fatherNode = node.SelectSingleNode("CHIL/_FREL");
                            XmlNode motherNode = node.SelectSingleNode("CHIL/_MREL");
                            var father = ParentalRelationship.GetRelationshipType(fatherNode);
                            var mother = ParentalRelationship.GetRelationshipType(motherNode);
                            Children.Add(child);
                            var parent = new ParentalRelationship(this, father, mother);
                            child.FamiliesAsChild.Add(parent);
                            AddParentAndChildrenFacts(child, Husband, father);
                            AddParentAndChildrenFacts(child, Wife, mother);
                        }
                        else
                            outputText.Report("Child not found in family :" + FamilyRef + "\n");
                    }
                    else
                        outputText.Report("Child without a reference found in family : " + FamilyRef + "\n");
                }

                AddFacts(node, Fact.ANNULMENT, outputText);
                AddFacts(node, Fact.DIVORCE, outputText);
                AddFacts(node, Fact.DIVORCE_FILED, outputText);
                AddFacts(node, Fact.ENGAGEMENT, outputText);
                AddFacts(node, Fact.MARRIAGE, outputText);
                AddFacts(node, Fact.MARRIAGE_BANN, outputText);
                AddFacts(node, Fact.MARR_CONTRACT, outputText);
                AddFacts(node, Fact.MARR_LICENSE, outputText);
                AddFacts(node, Fact.MARR_SETTLEMENT, outputText);
                AddFacts(node, Fact.SEPARATION, outputText);
                AddFacts(node, Fact.CENSUS, outputText);
                AddFacts(node, Fact.CUSTOM_EVENT, outputText);
                AddFacts(node, Fact.CUSTOM_FACT, outputText);
                AddFacts(node, Fact.REFERENCE, outputText);
                AddFacts(node, Fact.UNKNOWN, outputText);
                //TODO: need to think about family facts having AGE tags in GEDCOM
                if (HasGoodChildrenStatus)
                    CheckChildrenStatusCounts();
                if (Husband !=null && !Husband.IsMale)
                    Husband.QuestionGender(this, true);
                if (Wife != null && Wife.IsMale)
                    Wife.QuestionGender(this, false);
            }
        }

        private void CheckChildrenStatusCounts()
        {
            foreach (Fact f in ChildrenStatusFacts)
            {
                Match matcher = Fact.regexChildren1.Match(f.Comment);
                if (matcher.Success)
                    SetChildrenStatusCounts(matcher, 1, 2, 4);
                else
                {
                    matcher = Fact.regexChildren2.Match(f.Comment);
                    if (matcher.Success)
                        SetChildrenStatusCounts(matcher, 1, 3, 4);
                }
            }
        }

        private void SetChildrenStatusCounts(Match matcher, int totalGroup, int aliveGroup, int deadGroup)
        {
            int.TryParse(matcher.Groups[totalGroup].Value, out int resultT);
            ExpectedTotal += resultT;
            int.TryParse(matcher.Groups[aliveGroup].Value, out int resultA);
            ExpectedAlive += resultA;
            int.TryParse(matcher.Groups[deadGroup].Value, out int resultD);
            ExpectedDead += resultD;
        }

        private void AddParentAndChildrenFacts(Individual child, Individual parent, ParentalRelationship.ParentalRelationshipType prType)
        {
            if (parent != null)
            {
                string parentComment;
                string childrenComment;
                if (prType == ParentalRelationship.ParentalRelationshipType.UNKNOWN)
                {
                    parentComment = "Child of " + parent.IndividualID + ": " + parent.Name;
                    childrenComment = "Parent of " + child.IndividualID + ": " + child.Name;
                }
                else
                {
                    string titlecase = EnhancedTextInfo.ToTitleCase(prType.ToString().ToLower());
                    parentComment = titlecase + " child of " + parent.IndividualID + ": " + parent.Name;
                    childrenComment = titlecase + " parent of " + child.IndividualID + ": " + child.Name;
                }
                Fact parentFact = new Fact(parent.IndividualID, Fact.PARENT, child.BirthDate, child.BirthLocation, parentComment, true, true);
                Fact childrenFact = new Fact(child.IndividualID, Fact.CHILDREN, child.BirthDate, child.BirthLocation, childrenComment, true, true);
                child.AddFact(parentFact);
                parent.AddFact(childrenFact);
            }
        }

        public Family(Individual ind, string familyID)
            : this(familyID)
        {
            if (ind.IsMale)
                Husband = ind;
            else
                Wife = ind;
        }

        internal Family(Family f)
        {
            FamilyID = f.FamilyID;
            Facts = new List<Fact>(f.Facts);
            Husband = f.Husband == null ? null : new Individual(f.Husband);
            Wife = f.Wife == null ? null : new Individual(f.Wife);
            Children = new List<Individual>(f.Children);
            preferredFacts = new Dictionary<string, Fact>(f.preferredFacts);
            ExpectedTotal = f.ExpectedTotal;
            ExpectedAlive = f.ExpectedAlive;
            ExpectedDead = f.ExpectedDead;
            FamilyType = UNKNOWN;
        }

        private void AddFacts(XmlNode node, string factType, IProgress<string> outputText)
        {
            XmlNodeList list = node.SelectNodes(factType);
            bool preferredFact = true;
            foreach (XmlNode n in list)
            {
                Fact f = new Fact(n, this, preferredFact,outputText);
                if (f.FactType != Fact.CENSUS)
                {
                    Facts.Add(f);
                    if (!preferredFacts.ContainsKey(f.FactType))
                        preferredFacts.Add(f.FactType, f);
                }
                else
                {
                    // Handle a census fact on a family.
                    if (GeneralSettings.Default.OnlyCensusParents)
                    {
                        if (Husband != null && Husband.IsAlive(f.FactDate))
                            Husband.AddFact(f);
                        if (Wife != null && Wife.IsAlive(f.FactDate))
                            Wife.AddFact(f);
                    }
                    else
                    {  // all members of the family who are alive get the census fact
                        foreach (Individual person in Members)
                            if (person.IsAlive(f.FactDate))
                                person.AddFact(f);
                    }
                }
                preferredFact = false;
            }
        }

        public void FixFamilyID(int length)
        {
            try
            {
                if (FamilyID == null || FamilyID == string.Empty)
                {
                    FamilyType = SOLOINDIVIDUAL;
                    FamilyID = FamilyTree.Instance.NextSoloFamily;
                }
                else if(FamilyType.Equals(SOLOINDIVIDUAL) || FamilyType.Equals(PRE_MARRIAGE))
                    FamilyID = FamilyID.Substring(0, 2) + FamilyID.Substring(2).PadLeft(length, '0');
                else
                    FamilyID = FamilyID.Substring(0, 1) + FamilyID.Substring(1).PadLeft(length, '0');
            }
            catch (Exception)
            { // don't error if family ID is not of format Fxxxx
            }
        }

        /**
         * @return Returns the first fact of the given type.
         */
        public Fact GetPreferredFact(string factType)
        {
            return preferredFacts.ContainsKey(factType) ? preferredFacts[factType] : null;
        }

        /**
         * @return Returns the first fact of the given type.
         */
        public FactDate GetPreferredFactDate(string factType)
        {
            Fact f = GetPreferredFact(factType);
            return (f == null) ? FactDate.UNKNOWN_DATE : f.FactDate;
        }

        /**
         * @return Returns all facts of the given type.
         */
        public IEnumerable<Fact> GetFacts(string factType)
        {
            return Facts.Where(f => f.FactType == factType);
        }

        #region Properties

        public int FamilySize
        {
            get
            {
                int count = Children.Count;
                if (Husband != null)
                    count++;
                if (Wife != null)
                    count++;
                return count;
            }
        }

        public FactDate MarriageDate
        {
            get { return GetPreferredFactDate(Fact.MARRIAGE); }
        }

        public string MarriageLocation
        {
            get
            {
                Fact marriage = GetPreferredFact(Fact.MARRIAGE);
                return (marriage == null) ? string.Empty : marriage.Location.ToString();
            }
        }

        public string MaritalStatus
        {
            get
            {
                if (Husband == null || Wife == null || !MarriageDate.IsKnown)
                    return SINGLE;
                else
                {
                    foreach (Fact f in Facts)
                    {
                        if (f.FactType == Fact.MARR_CONTRACT || f.FactType == Fact.MARR_LICENSE || f.FactType == Fact.MARR_SETTLEMENT
                            || f.FactType == Fact.MARRIAGE || f.FactType == Fact.MARRIAGE_BANN)
                            return MARRIED;
                    }
                    return UNMARRIED;
                }
            }
        }

        public string HusbandID { get { return (Husband == null) ? string.Empty : Husband.IndividualID; } }

        public string WifeID { get { return (Wife == null) ? string.Empty : Wife.IndividualID; } }

        public IEnumerable<Individual> Members
        {
            get
            {
                if (Husband != null) yield return Husband;
                if (Wife != null) yield return Wife;
                if (Children != null && Children.Count > 0)
                    foreach (Individual child in Children) yield return child;
            }
        }

        public IEnumerable<int> RelationTypes
        {
            get
            {
                if (Husband != null) yield return Husband.RelationType;
                if (Wife != null) yield return Wife.RelationType;
                if (Children != null && Children.Count > 0)
                    foreach (Individual child in Children) yield return child.RelationType;
            }
        }

        public string FamilyName
        {
            get
            {
                string husbandsName = Husband == null ? "Unknown" : Husband.Name;
                string wifesName = Wife == null ? "Unknown" : Wife.Name;
                return husbandsName + " and " + wifesName;
            }
        }

        public string MarriageFilename
        {
            get
            {
                return FamilyTree.ValidFilename(FamilyID + " - Marriage of " + FamilyName + ".html");
            }
        }

        public string ChildrenFilename
        {
            get
            {
                return FamilyTree.ValidFilename(FamilyID + " - Children of " + FamilyName + ".html");
            }
        }

        public string FamilyRef
        {
            get
            {
                if (FamilyType.Equals(SOLOINDIVIDUAL))
                    return "Solo Family " + FamilyID + ": " + (Husband == null ? string.Empty : Husband.Name) + (Wife == null ? string.Empty : Wife.Name);
                else 
                    return FamilyID + ": " + FamilyName;
            }
        }

        public Individual Spouse(Individual ind)
        {
            if (ind.Equals(Husband))
                return Wife;
            if (ind.Equals(Wife))
                return Husband;
            return null;
        }

        public bool ContainsSurname(string surname)
        {
            return Members.Any(x => x.Surname.Equals(surname, StringComparison.InvariantCultureIgnoreCase));
        }

        public bool On1911Census
        {
            get
            {
                if (Husband != null && Husband.IsCensusDone(CensusDate.UKCENSUS1911)) return true;
                if (Wife != null && Wife.IsCensusDone(CensusDate.UKCENSUS1911)) return true;
                return false;
            }
        }

        // only check shared facts for children status
        private IEnumerable<Fact> ChildrenStatusFacts { get { return Facts.Where(f => f.FactType == Fact.CHILDREN1911 && f.FactErrorLevel == Fact.FactError.GOOD); } }

        public bool HasGoodChildrenStatus { get { return Facts.Any(f => f.FactType == Fact.CHILDREN1911 && f.FactErrorLevel == Fact.FactError.GOOD); } }

        public bool HasAnyChildrenStatus { get { return Facts.Any(f => f.FactType == Fact.CHILDREN1911); } }
        #endregion

        public void SetBudgieCode(Individual ind, int lenAhnentafel)
        {
            Individual spouse = ind.IsMale ? Wife : Husband;
            if (spouse != null && spouse.BudgieCode == string.Empty)
            {
                spouse.BudgieCode = ind.BudgieCode + "*s";
            }
            int directChild = 0;
            if (ind.RelationType == Individual.DIRECT)
            {
                //first find which child is a direct
                foreach (Individual child in Children.OrderBy(c => c.BirthDate))
                {
                    directChild++;
                    if (child.RelationType == Individual.DIRECT)
                        break;
                }
            }
            if (directChild > 0)
            {
                int childcount = 0;
                foreach (Individual child in Children.OrderBy(c => c.BirthDate))
                {
                    childcount++;
                    if (child.BudgieCode == string.Empty)
                    {
                        string prefix = (directChild < childcount) ? "+" : "-";
                        string code = (Math.Abs(directChild - childcount)).ToString();
                        string ahnentafel = ((Int64)Math.Floor(ind.Ahnentafel / 2.0)).ToString();
                        child.BudgieCode = ahnentafel.PadLeft(lenAhnentafel, '0') + prefix + code.PadLeft(2, '0');
                    }
                }
            }
            else
            {   // we have got here because we are not dealing with a direct nor a family that contains a direct child
                int childcount = 0;
                foreach (Individual child in Children.OrderBy(c => c.BirthDate))
                {
                    childcount++;
                    if (child.BudgieCode == string.Empty)
                    {
                        child.BudgieCode = ind.BudgieCode + "." + childcount.ToString().PadLeft(2, '0');
                    }
                }
            }
        }

        public void SetSpouseRelation(Individual ind, int relationType)
        {
            Individual spouse = ind.IsMale ? Wife : Husband;
            if (spouse != null)
                spouse.RelationType = relationType;
        }

        public void SetChildRelation(Queue<Individual> queue, int relationType)
        {
            foreach (Individual child in Children)
            {
                int previousType = child.RelationType;
                child.RelationType = relationType;
                if (child.RelationType != previousType)
                {
                    // add this changed individual to list 
                    // of relatives to update family of
                    queue.Enqueue(child);
                }
            }
        }

        public void SetChildrenCommonRelation(Individual parent, CommonAncestor commonAncestor)
        {
            foreach (Individual child in Children)
                if (child.CommonAncestor == null || child.CommonAncestor.Distance > commonAncestor.Distance + 1)
                    child.CommonAncestor = new CommonAncestor(commonAncestor.Ind, commonAncestor.Distance + 1, !child.IsNaturalChildOf(parent) || commonAncestor.Step);
        }

        #region IDisplayFamily Members

        string IDisplayFamily.Husband
        {
            get { return Husband == null ? string.Empty : Husband.Name + " (b." + Husband.BirthDate + ")"; }
        }

        string IDisplayFamily.Wife
        {
            get { return Wife == null ? string.Empty : Wife.Name + " (b." + Wife.BirthDate + ")"; }
        }

        public string Marriage
        {
            get
            {
                return ToString();
            }
        }

        string IDisplayFamily.Children
        {
            get
            {
                StringBuilder result = new StringBuilder();
                foreach (Individual c in Children)
                {
                    if (result.Length > 0)
                        result.Append(", ");
                    result.Append(c.Name + " (b." + c.BirthDate + ")");
                }
                return result.ToString();
            }
        }

        public FactDate FamilyDate
        {
            get
            {
                // return "central" date of family - use marriage facts, Husband/Wife facts, children birth facts
                List<FactDate> dates = new List<FactDate>();
                foreach (Fact f in Facts)
                    if (f.FactDate.AverageDate.IsKnown)
                        dates.Add(f.FactDate.AverageDate);
                if (Husband != null)
                    foreach (Fact f in Husband.PersonalFacts)
                        if (f.FactDate.AverageDate.IsKnown)
                            dates.Add(f.FactDate.AverageDate);
                if (Wife != null)
                    foreach (Fact f in Wife.PersonalFacts)
                        if (f.FactDate.AverageDate.IsKnown)
                            dates.Add(f.FactDate.AverageDate);
                foreach (Individual c in Children)
                    if (c.BirthDate.AverageDate.IsKnown)
                        dates.Add(c.BirthDate.AverageDate);
                if (dates.Count == 0)
                    return FactDate.UNKNOWN_DATE;
                long averageTicks = 0L;
                foreach (FactDate fd in dates)
                    averageTicks += fd.StartDate.Ticks / dates.Count;
                try
                {
                    DateTime averageDate = new DateTime(averageTicks);
                    return new FactDate(averageDate, averageDate);
                }
                catch (ArgumentOutOfRangeException)
                {
                }
                return FactDate.UNKNOWN_DATE;
            }
        }

        public FactLocation Location
        {
            get
            {
                return FactLocation.BestLocation(AllFamilyFacts, FamilyDate);
            }
        }

        #endregion

        public bool IsAtLocation(FactLocation loc, int level)
        {
            foreach (Fact f in AllFamilyFacts)
            {
                if (f.Location.Equals(loc, level))
                    return true;
            }
            return false;
        }
 
        public bool BothParentsAlive(FactDate when)
        {
            if (Husband == null || Wife == null || FamilyType.Equals(SOLOINDIVIDUAL))
                return false;
            return Husband.IsAlive(when) && Wife.IsAlive(when) && Husband.GetAge(when).MinAge > 13 && Wife.GetAge(when).MinAge > 13;
        }

        private IEnumerable<Fact> AllFamilyFacts
        {
            get
            {
                List<IList<Fact>> results = new List<IList<Fact>>
                {
                    // add the family facts then the facts from each individual
                    Facts
                };
                if (Husband != null)
                    results.Add(Husband.PersonalFacts);
                if (Wife != null)
                    results.Add(Wife.PersonalFacts);
                foreach (Individual c in Children)
                    results.Add(c.PersonalFacts);
                return results.SelectMany(x => x);
            }
        }

        public IEnumerable<DisplayFact> AllDisplayFacts
        {
            get
            {
                List<DisplayFact> results = new List<DisplayFact>();
                string surname, forenames;
                if (Husband == null)
                {
                    if (Wife == null)
                    {
                        surname = string.Empty;
                        forenames = string.Empty;
                    }
                    else
                    {
                        surname = Wife.Surname;
                        forenames = Wife.Forenames;
                    }
                }
                else
                {
                    if (Wife == null)
                    {
                        surname = Husband.Surname;
                        forenames = Husband.Forenames;
                    }
                    else
                    {
                        surname = Husband.Surname;
                        forenames = Husband.Forenames + " & " + Wife.Forenames;
                    }
                }
                foreach (Fact f in Facts)
                    results.Add(new DisplayFact(null, surname, forenames, f));
                if (Husband != null)
                    foreach (Fact f in Husband.PersonalFacts)
                        results.Add(new DisplayFact(Husband, f));
                if (Wife != null)
                    foreach (Fact f in Wife.PersonalFacts)
                        results.Add(new DisplayFact(Wife, f));
                foreach (Individual child in Children)
                {
                    foreach (Fact f in child.GetFacts(Fact.BIRTH))
                        results.Add(new DisplayFact(child, f));
                    foreach (Fact f in child.GetFacts(Fact.BAPTISM))
                        results.Add(new DisplayFact(child, f));
                    foreach (Fact f in child.GetFacts(Fact.CHRISTENING))
                        results.Add(new DisplayFact(child, f));
                }
                return results;
            }
        }

        public override string ToString()
        {
            Fact marriage = GetPreferredFact(Fact.MARRIAGE);
            if (marriage == null)
                return string.Empty;
            if (marriage.Location.IsBlank)
                return MarriageDate.ToString();
            else
                return MarriageDate.ToString() + " at " + marriage.Location;
        }
    }
}