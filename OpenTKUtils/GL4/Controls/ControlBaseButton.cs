﻿/*
 * Copyright 2019-2020 Robbyxp1 @ github.com
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
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTKUtils.GL4.Controls
{
    public abstract class GLButtonBase : GLImageBase
    {
        public Color ForeColor { get { return foreColor; } set { foreColor = value; Invalidate(); } }       // of text
        public Color ButtonBackColor { get { return buttonBackColor; } set { buttonBackColor = value; Invalidate(); } }
        public Color MouseOverBackColor { get { return mouseOverBackColor; } set { mouseOverBackColor = value; Invalidate(); } }
        public Color MouseDownBackColor { get { return mouseDownBackColor; } set { mouseDownBackColor = value; Invalidate(); } }
        public float BackColorScaling { get { return backColorScaling; } set { backColorScaling = value; Invalidate(); } }

        public GLButtonBase(string name, Rectangle window, Color backcolor) : base(name, window, backcolor)
        {
            InvalidateOnEnterLeave = true;
            InvalidateOnMouseDownUp = true;
        }

        private Color buttonBackColor { get; set; } = Color.Gray;
        private Color mouseOverBackColor { get; set; } = Color.Green;
        private Color mouseDownBackColor { get; set; } = Color.YellowGreen;
        private Color foreColor { get; set; } = Color.Black;
        private float backColorScaling = 0.5F;

    }

    public abstract class GLButtonTextBase : GLButtonBase
    {
        public string Text { get { return text; } set { text = value; Invalidate(); } }
        public ContentAlignment TextAlign { get { return textAlign; } set { textAlign = value; Invalidate(); } }

        public GLButtonTextBase(string name, Rectangle window, Color backcolor) : base(name, window, backcolor)
        {
        }

        protected string TextNI { set { text = value; } }

        private string text;
        private ContentAlignment textAlign { get; set; } = ContentAlignment.MiddleCenter;

    }

}
