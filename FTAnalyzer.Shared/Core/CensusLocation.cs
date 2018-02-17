﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace FTAnalyzer
{
    public class CensusLocation
    {
        private static Dictionary<Tuple<string, string>, CensusLocation> CENSUSLOCATIONS = new Dictionary<Tuple<string, string>, CensusLocation>();
        public static readonly CensusLocation UNKNOWN = new CensusLocation(string.Empty);
        public static readonly CensusLocation SCOTLAND = new CensusLocation(Countries.SCOTLAND);
        public static readonly CensusLocation UNITED_STATES = new CensusLocation(Countries.UNITED_STATES);
        public static readonly CensusLocation CANADA = new CensusLocation(Countries.CANADA);
        public string Year { get; private set; }
        public string Piece { get; private set; }
        public string RegistrationDistrict { get; private set; }
        public string Parish { get; private set; }
        public string County { get; private set; }
        public string Location { get; private set; }

        static CensusLocation()
        {
            LoadCensusLocationFile(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location));
#region Test CensusLocation.xml file
            //foreach(KeyValuePair<Tuple<string, string>, CensusLocation> kvp in CENSUSLOCATIONS)
            //{
            //    FactLocation.GetLocation(kvp.Value.Location); // force creation of location facts
            //}
#endregion
        }

        public static void LoadCensusLocationFile(string startPath)
        {
            #region Census Locations
            // load Census Locations from XML file
            if (startPath == null) return;
            string filename = Path.Combine(startPath, @"Resources\CensusLocations.xml");
            if (File.Exists(filename))
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filename);
                //xmlDoc.Validate(something);
                foreach (XmlNode n in xmlDoc.SelectNodes("CensusLocations/Location"))
                {
                    string year = n.Attributes["Year"].Value;
                    string piece = n.Attributes["Piece"].Value;
                    string RD = n.Attributes["RD"].Value;
                    string parish = n.Attributes["Parish"].Value;
                    string county = n.Attributes["County"].Value;
                    string location = n.InnerText;
                    CensusLocation cl = new CensusLocation(year, piece, RD, parish, county, location);
                    CENSUSLOCATIONS.Add(new Tuple<string, string>(year, piece), cl);
                }
            }
            #endregion
        }

        public CensusLocation(string location) : this(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, location) { }

        public CensusLocation(string year, string piece, string rd, string parish, string county, string location)
        {
            this.Year = year;
            this.Piece = piece;
            this.RegistrationDistrict = rd;
            this.Parish = parish;
            this.County = county;
            this.Location = location;
        }

        public static CensusLocation GetCensusLocation(string year, string piece)
        {
            Tuple<string, string> key = new Tuple<string, string>(year, piece);
            CENSUSLOCATIONS.TryGetValue(key, out CensusLocation result);
            return result ?? CensusLocation.UNKNOWN;
        }

        public override string ToString()
        {
            return this.Location.Equals(string.Empty) ? "UNKNOWN" : this.Location;
        }
    }
}
