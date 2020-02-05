﻿/*
 * Copyright © 2018-2019 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTKUtils
{
    public class LineReader : IDisposable
    {
        class Stack : IDisposable
        {
            public Stack( TextReader s, string p )
            {
                SR = s; Path=p;
            }

            public TextReader SR;
            public string Path;

            public void Dispose()
            {
                SR.Dispose();
            }
        };

        private List<Stack> filestack = new List<Stack>();

        public string CurrentFile { get { return filestack.Count > 0 ? filestack.Last().Path : null; } }

        public bool Open(string path)       // can open on top to produce a include file stack
        {
            try
            {
                var utc8nobom = new UTF8Encoding(false);        // give it the default UTF8 no BOM encoding, it will detect BOM or UCS-2 automatically

                StreamReader sr = new StreamReader(path, utc8nobom);

                if ( sr != null )
                {
                    filestack.Add(new Stack(sr, path));
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public void OpenString( string text )
        {
            StringReader tr = new StringReader(text);
            filestack.Add(new Stack(tr, "string"));
        }

        public string ReadLine()
        {
            try
            {
                while (filestack.Count > 0)
                {
                    string s = filestack.Last().SR.ReadLine();

                    if (s == null)
                    {
                        filestack.Last().Dispose();
                        filestack.Remove(filestack.Last());
                    }
                    else
                        return s;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (filestack != null)
            {
                foreach (var s in filestack)
                {
                    s.Dispose();
                }
            }
        }
    }
}
