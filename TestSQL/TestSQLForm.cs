﻿using EliteDangerousCore;
using EliteDangerousCore.DB;
using EMK.LightGeometry;
using QuickJSON;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace TestSQL
{
    public partial class TestSQLForm : Form
    {
        public TestSQLForm()
        {
            InitializeComponent();

            SystemsDatabase.Instance.MaxThreads = 8;
            SystemsDatabase.Instance.MinThreads = 2;
            SystemsDatabase.Instance.MultiThreaded = true;
            SystemsDatabase.Instance.Initialize();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            SystemsDatabase.Instance.Stop();
        }

        private void buttonLoadEDSM_Click(object sender, System.EventArgs e)
        {
            SystemsDatabase.Instance.Stop();

            BaseUtils.FileHelpers.DeleteFileNoError(EliteDangerousCore.EliteConfigInstance.InstanceOptions.SystemDatabasePath);

            SystemsDatabase.Reset();
            SystemsDatabase.Instance.MaxThreads = 8;
            SystemsDatabase.Instance.MinThreads = 2;
            SystemsDatabase.Instance.MultiThreaded = true;
            SystemsDatabase.Instance.Initialize();

            string edsminfile = @"c:\code\examples\edsm\edsmsystems.10e6.json";

            SystemsDatabase.Instance.MakeSystemTableFromFile(edsminfile, null, () => false, (s) => System.Diagnostics.Debug.WriteLine(s));
            AddText("EDSM DB Made");
        }

        private void buttonTestEDSM_Click(object sender, System.EventArgs argse)
        {
            bool printstars = false;
            bool testdelete = false;

            if (printstars)
            {
                using (StreamWriter wr = new StreamWriter(@"c:\code\edsm\starlistout.lst"))
                {
                    SystemsDB.ListStars(orderby: "s.sectorid,s.edsmid", starreport: (s) =>
                    {
                        wr.WriteLine(s.Name + " " + s.Xi + "," + s.Yi + "," + s.Zi + " Grid:" + s.GridID);
                    });
                }
            }

            if (testdelete)
            {
                SystemsDB.RemoveGridSystems(new int[] { 810, 911 });

                using (StreamWriter wr = new StreamWriter(@"c:\code\edsm\starlistout2.lst"))
                {
                    SystemsDB.ListStars(orderby: "s.sectorid,s.edsmid", starreport: (s) =>
                    {
                        wr.WriteLine(s.Name + " " + s.Xi + "," + s.Yi + "," + s.Zi + " Grid:" + s.GridID);
                    });
                }
            }

            // ********************************************
            // TESTS BASED on the 10e6 json file
            // ********************************************

            {
                BaseUtils.AppTicks.TickCountLap();
                ISystem s;

                for (int I = 0; I < 50; I++)    // 6/4/18 50 @ 38       (76 no index on systems)
                {
                    s = SystemsDB.FindStar("HIP 112535");       // this one is at the front of the DB
                    System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("HIP 112535"));
                }

                AddText("FindStar HIP x for X: " + BaseUtils.AppTicks.TickCountLap());
            }


            {
                ISystem s;

                BaseUtils.AppTicks.TickCountLap();
                string star = "HIP 101456";
                for (int I = 0; I < 50; I++)
                {
                    s = SystemsDB.FindStar(star);       // This one is at the back of the DB
                    System.Diagnostics.Debug.Assert(s != null && s.Name.Equals(star));
                    //   AddText("Lap : " + BaseUtils.AppTicks.TickCountLap());
                }

                AddText("Find Standard for X: " + BaseUtils.AppTicks.TickCountLap());
            }

            {
                ISystem s;

                BaseUtils.AppTicks.TickCountLap();

                for (int I = 0; I < 50; I++)        // 6/4/18 50 @ 26ms (No need for system index)
                {
                    s = SystemsDB.FindStar("kanur");
                    System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Kanur") && s.Xi == -2832 && s.Yi == -3188 && s.Zi == 12412);
                }

                AddText("Find Kanur for X: " + BaseUtils.AppTicks.TickCountLap());
            }

            {
                ISystem s;
                s = SystemsDB.FindStar("hip 91507");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("HIP 91507"));
                s = SystemsDB.FindStar("Byua Eurk GL-Y d107");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Byua Eurk GL-Y d107") && s.X == -3555.5625 && s.Y == 119.25 && s.Z == 5478.59375);
                s = SystemsDB.FindStar("BD+18 711");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("BD+18 711") && s.Xi == 1700 && s.Yi == -68224 && s.Zi == -225284);
                s = SystemsDB.FindStar("Chamaeleon Sector FG-W b2-3");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Chamaeleon Sector FG-W b2-3") && s.Xi == 71440 && s.Yi == -12288 && s.Zi == 35092 && s.MainStarType == EDStar.Unknown && s.SystemAddress == null);
            }


            { // No system indexes = 4179  xz=10 @21, xz=100 @ 176,  x= 100 @ 1375, xz 100 @ 92 xz vacummed 76.
                AddText("Begin Find Pos for 100");
                ISystem s;
                BaseUtils.AppTicks.TickCountLap();

                for (int I = 0; I < 100; I++)
                {
                    SystemsDatabase.Instance.DBRead(db =>
                    {
                        s = SystemsDB.GetSystemByPosition(-100.7, 166.4, -36.8, db);
                        System.Diagnostics.Debug.Assert(s != null && s.Name == "Col 285 Sector IZ-B b15-2");
                    });

                    //  AddText("Lap : " + BaseUtils.AppTicks.TickCountLap());
                }

                AddText("Find Pos for 100: " + BaseUtils.AppTicks.TickCountLap());
            }


            {
                ISystem s;
                s = SystemCache.FindSystem("hip 91507");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("HIP 91507"));
                s = SystemCache.FindSystem("hip 91507");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("HIP 91507"));
                s = SystemCache.FindSystem("Byua Eurk GL-Y d107");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Byua Eurk GL-Y d107") && s.X == -3555.5625 && s.Y == 119.25 && s.Z == 5478.59375);
                s = SystemCache.FindSystem("BD+18 711");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("BD+18 711") && s.Xi == 1700 && s.Yi == -68224 && s.Zi == -225284);
                s = SystemCache.FindSystem("Chamaeleon Sector FG-W b2-3");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Chamaeleon Sector FG-W b2-3") && s.Xi == 71440 && s.Yi == -12288 && s.Zi == 35092);
                s = SystemCache.FindSystem("kanur");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Kanur") && s.Xi == -2832 && s.Yi == -3188 && s.Zi == 12412);
            }

            {
                List<ISystem> slist;

                BaseUtils.AppTicks.TickCountLap();

                for (int I = 0; I < 10; I++)
                {
                    slist = SystemsDB.FindStarWildcard("Tucanae Sector CQ-Y");
                    System.Diagnostics.Debug.Assert(slist != null && slist.Count > 20);
                    //AddText("Lap : " + BaseUtils.AppTicks.TickCountLap());
                }

                AddText("Find Wildcard Standard trunced: " + BaseUtils.AppTicks.TickCountLap());
            }


            {
                BaseUtils.AppTicks.TickCountLap();
                List<ISystem> slist;
                for (int I = 0; I < 10; I++)
                {
                    slist = SystemsDB.FindStarWildcard("HIP 6");
                    System.Diagnostics.Debug.Assert(slist != null && slist.Count > 48);
                    foreach (var e in slist)
                        System.Diagnostics.Debug.Assert(e.Name.StartsWith("HIP 6"));
                }

                AddText("Find Wildcard HIP 6: " + BaseUtils.AppTicks.TickCountLap());
            }

            {
                BaseUtils.AppTicks.TickCountLap();
                List<ISystem> slist;
                for (int I = 0; I < 10; I++)
                {
                    slist = SystemsDB.FindStarWildcard("USNO-A2.0 127");
                    System.Diagnostics.Debug.Assert(slist != null && slist.Count > 185);
                    foreach (var e in slist)
                        System.Diagnostics.Debug.Assert(e.Name.StartsWith("USNO-A2.0"));
                }

                AddText("Find Wildcard USNo: " + BaseUtils.AppTicks.TickCountLap());
            }

            {
                List<ISystem> slist;
                BaseUtils.AppTicks.TickCountLap();

                for (int I = 0; I < 1; I++)
                {
                    slist = SystemsDB.FindStarWildcard("HIP");
                    System.Diagnostics.Debug.Assert(slist != null && slist.Count > 48);
                }

                AddText("Find Wildcard HIP: " + BaseUtils.AppTicks.TickCountLap());

            }
            {
                List<ISystem> slist;

                slist = SystemsDB.FindStarWildcard("Synuefai MC-H");
                System.Diagnostics.Debug.Assert(slist != null && slist.Count >= 3);
                slist = SystemsDB.FindStarWildcard("Synuefai MC-H c");
                System.Diagnostics.Debug.Assert(slist != null && slist.Count >= 3);
                slist = SystemsDB.FindStarWildcard("Synuefai MC-H c12");
                System.Diagnostics.Debug.Assert(slist != null && slist.Count == 0);
                slist = SystemsDB.FindStarWildcard("Synuefai MC-H c12-");
                System.Diagnostics.Debug.Assert(slist != null && slist.Count >= 3);

                slist = SystemsDB.FindStarWildcard("HIP 6");
                System.Diagnostics.Debug.Assert(slist != null && slist.Count > 5);

                slist = SystemsDB.FindStarWildcard("Coalsack Sector");
                System.Diagnostics.Debug.Assert(slist != null && slist.Count >= 4);

                slist = SystemsDB.FindStarWildcard("Coalsack");
                System.Diagnostics.Debug.Assert(slist != null && slist.Count >= 4);

                slist = SystemsDB.FindStarWildcard("4 S");
                System.Diagnostics.Debug.Assert(slist != null && slist.Count >= 1);

            }

            {   // xz index = 70ms
                BaseUtils.SortedListDoubleDuplicate<ISystem> list = new BaseUtils.SortedListDoubleDuplicate<ISystem>();

                BaseUtils.AppTicks.TickCountLap();
                double x = 0, y = 0, z = 0;

                SystemsDatabase.Instance.DBRead(db =>
                {
                    SystemsDB.GetSystemListBySqDistancesFrom(x, y, z, 20000, 0.5, 20, true, db, (dist, sys) => { list.Add(dist, sys); });
                    System.Diagnostics.Debug.Assert(list != null && list.Count >= 20);
                });

                //foreach (var k in list)   AddText(Math.Sqrt(k.Key).ToString("N1") + " Star " + k.Value.ToStringVerbose());
            }

            { // xz index = 185ms
                BaseUtils.SortedListDoubleDuplicate<ISystem> list = new BaseUtils.SortedListDoubleDuplicate<ISystem>();

                BaseUtils.AppTicks.TickCountLap();
                double x = 490, y = 0, z = 0;

                SystemsDatabase.Instance.DBRead(db =>
                {
                    SystemsDB.GetSystemListBySqDistancesFrom(x, y, z, 20000, 0.5, 50, true, db, (dist, sys) => { list.Add(dist, sys); }); //should span 2 grids 810/811
                    System.Diagnostics.Debug.Assert(list != null && list.Count >= 20);
                });

                //foreach (var k in list) AddText(Math.Sqrt(k.Key).ToString("N1") + " Star " + k.Value.ToStringVerbose());
            }

            { // 142ms with xz and no sector lookup
                BaseUtils.AppTicks.TickCountLap();
                ISystem s;
                s = SystemCache.GetSystemNearestTo(new Point3D(100, 0, 0), new Point3D(1, 0, 0), 110, 20, SystemCache.SystemsNearestMetric.IterativeWaypointDevHalf, 1);
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Alpha Centauri"));
                AddText("Find Nearest Star: " + BaseUtils.AppTicks.TickCountLap());
            }

            {
                SystemCache.AddToAutoCompleteList(new List<string>() { "galone", "galtwo", "sol2" });
                SortedSet<string> sys = new SortedSet<string>();
                SystemCache.ReturnSystemAutoCompleteList("Sol", null, sys);
                System.Diagnostics.Debug.Assert(sys != null && sys.Contains("Solati") && sys.Count >= 4);
            }


            {
                var v = SystemsDB.GetStarPositions(5, (x, y, z) => { return new Vector3((float)x / 128.0f, (float)y / 128.0f, (float)z / 128.0f); });
                System.Diagnostics.Debug.Assert(v.Count > 450000);
                //       var v2 = SystemClassDB.GetStarPositions(100, (x, y, z) => { return new Vector3((float)x / 128.0f, (float)y / 128.0f, (float)z / 128.0f); });
            }

            AddText($"EDSM Test Complete");
        }

        void AddText(string s)
        {
            richTextBox1.Text += s;
            richTextBox1.Text += Environment.NewLine;
            richTextBox1.ScrollToCaret();
        }

        private void buttonReloadSpansh_Click(object sender, EventArgs e)
        {
            SystemsDatabase.Instance.Stop();

            BaseUtils.FileHelpers.DeleteFileNoError(EliteDangerousCore.EliteConfigInstance.InstanceOptions.SystemDatabasePath);

            SystemsDatabase.Reset();
            SystemsDatabase.Instance.MaxThreads = 8;
            SystemsDatabase.Instance.MinThreads = 2;
            SystemsDatabase.Instance.MultiThreaded = true;
            SystemsDatabase.Instance.Initialize();

            string edsminfile = @"c:\code\examples\edsm\systems_1week.json";

            SystemsDatabase.Instance.MakeSystemTableFromFile(edsminfile, null, () => false, (s) => System.Diagnostics.Debug.WriteLine(s));
            AddText("Spansh DB Made");
        }

        private void buttonTestSpansh_Click(object sender, EventArgs e)
        {
            {
                // this tests DBGetStars both types of constructor at the bottom of the file

                ISystem s;
                s = SystemsDB.FindStar("Herschel 36");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Herschel 36") && s.MainStarType == EDStar.O);
                s = SystemsDB.FindStar("Piscium Sector GS-J a9-1"); // "id64":22954989341528
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Piscium Sector GS-J a9-1") && s.X == -36.25 && s.Y == -44.78125 && s.Z == 5.4375 && s.SystemAddress == 22954989341528 && s.MainStarType == EDStar.Unknown);
                s = SystemsDB.FindStar("Ogairy ET-X c28-537"); //"id64":147694161044218
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Ogairy ET-X c28-537") && s.X == 526.5625 && s.Y == 70.03125 && s.Z == 20687.625 && s.SystemAddress == 147694161044218);
                s = SystemsDB.FindStar("S171 15");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("S171 15") && s.SystemAddress == 61177999 && s.MainStarType == EDStar.O);
                s = SystemsDB.FindStar("44 Iota Orionis");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("44 Iota Orionis") && s.SystemAddress == 2587791 && s.X == 1094.0625 && s.Y == -759.53125 && s.Z == -1911.5 && s.MainStarType == EDStar.O);
                s = SystemsDB.FindStar("Pleiades Sector XP-O b6-3");
                System.Diagnostics.Debug.Assert(s != null && s.Name.Equals("Pleiades Sector XP-O b6-3") && s.SystemAddress == 7266949997873 && s.MainStarType == EDStar.M);

                
            }

            AddText($"Spansh Test Complete");
        }


    }
}
