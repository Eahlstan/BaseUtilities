﻿/*
 * Copyright © 2022 - 2022 EDDiscovery development team
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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static partial class DrawingHelpersStaticFunc
{
    #region Content Align

    static public StringFormat StringFormatFromContentAlignment(ContentAlignment c)
    {
        StringFormat f = new StringFormat();
        if (c == ContentAlignment.BottomCenter || c == ContentAlignment.MiddleCenter || c == ContentAlignment.TopCenter)
            f.Alignment = StringAlignment.Center;
        else if (c == ContentAlignment.BottomLeft || c == ContentAlignment.MiddleLeft || c == ContentAlignment.TopLeft)
            f.Alignment = StringAlignment.Near;
        else
            f.Alignment = StringAlignment.Far;

        if (c == ContentAlignment.BottomCenter || c == ContentAlignment.BottomLeft || c == ContentAlignment.BottomRight)
            f.LineAlignment = StringAlignment.Far;
        else if (c == ContentAlignment.MiddleLeft || c == ContentAlignment.MiddleCenter || c == ContentAlignment.MiddleRight)
            f.LineAlignment = StringAlignment.Center;
        else
            f.LineAlignment = StringAlignment.Near;

        return f;
    }

    static public Rectangle ImagePositionFromContentAlignment(this ContentAlignment c, Rectangle client, Size image, bool cliptorectangle = false)
    {
        int left = client.Left;

        if (c == ContentAlignment.BottomCenter || c == ContentAlignment.MiddleCenter || c == ContentAlignment.TopCenter)
            left += Math.Max((client.Width - image.Width) / 2, 0);
        else if (c == ContentAlignment.BottomLeft || c == ContentAlignment.MiddleLeft || c == ContentAlignment.TopLeft)
            left += 0;
        else
            left += Math.Max(client.Width - image.Width, 0);

        int top = client.Top;

        if (c == ContentAlignment.BottomCenter || c == ContentAlignment.BottomLeft || c == ContentAlignment.BottomRight)
            top += Math.Max(client.Height - image.Height, 0);
        else if (c == ContentAlignment.MiddleLeft || c == ContentAlignment.MiddleCenter || c == ContentAlignment.MiddleRight)
            top += Math.Max((client.Height - image.Height) / 2, 0);
        else
            top += 0;

        if (cliptorectangle)        // ensure we start in rectangle..
        {
            left = Math.Max(0, left);
            top = Math.Max(0, top);
        }

        return new Rectangle(left, top, image.Width, image.Height);
    }

    #endregion

    #region Rectangles

    public static int XCenter(this Rectangle r)
    {
        return (r.Right + r.Left) / 2;
    }

    public static int YCenter(this Rectangle r)
    {
        return (r.Top + r.Bottom) / 2;
    }

    public static Point ClipTo(this Point p, Rectangle r,int offsetrightbottom = -1)
    {
        return new Point(p.X.Range(r.Left, r.Right + offsetrightbottom), p.Y.Range(r.Top, r.Bottom + offsetrightbottom));
    }

    static public GraphicsPath RectCutCorners(int x, int y, int width, int height, int roundnessleft, int roundnessright)
    {
        GraphicsPath gr = new GraphicsPath();

        gr.AddLine(x + roundnessleft, y, x + width - 1 - roundnessright, y);
        gr.AddLine(x + width - 1, y + roundnessright, x + width - 1, y + height - 1 - roundnessright);
        gr.AddLine(x + width - 1 - roundnessright, y + height - 1, x + roundnessleft, y + height - 1);
        gr.AddLine(x, y + height - 1 - roundnessleft, x, y + roundnessleft);
        gr.AddLine(x, y + roundnessleft, x + roundnessleft, y);         // close figure manually, closing it with a break does not seem to work
        return gr;
    }

    // produce a rounded rectangle with a cut out at the top..

    static public GraphicsPath RectCutCorners(int x, int y, int width, int height, int roundnessleft, int roundnessright, int topcutpos, int topcutlength)
    {
        GraphicsPath gr = new GraphicsPath();

        if (topcutlength > 0)
        {
            gr.AddLine(x + roundnessleft, y, x + topcutpos, y);
            gr.StartFigure();
            gr.AddLine(x + topcutpos + topcutlength, y, x + width - 1 - roundnessright, y);
        }
        else
            gr.AddLine(x + roundnessleft, y, x + width - 1 - roundnessright, y);

        gr.AddLine(x + width - 1, y + roundnessright, x + width - 1, y + height - 1 - roundnessright);
        gr.AddLine(x + width - 1 - roundnessright, y + height - 1, x + roundnessleft, y + height - 1);
        gr.AddLine(x, y + height - 1 - roundnessleft, x, y + roundnessleft);
        gr.AddLine(x, y + roundnessleft, x + roundnessleft, y);         // close figure manually, closing it with a break does not seem to work
        return gr;
    }

    #endregion

    #region Misc

    // this scales the font down only to fit into textarea given a graphic and text.  Used in Paint
    // fnt itself is not deallocated.
    public static Font GetFontToFit(this Graphics g, string text, Font fnt, Size textarea, StringFormat fmt)
    {
        if (!text.HasChars())       // can't tell
            return fnt;

        bool ownfont = false;
        while (true)
        {
            SizeF drawnsize = g.MeasureString(text, fnt, new Point(0, 0), fmt);

            if (fnt.Size < 2 || ((int)(drawnsize.Width + 0.99f) <= textarea.Width && (int)(drawnsize.Height + 0.99f) <= textarea.Height))
                return fnt;

            if (ownfont)
                fnt.Dispose();

            fnt = BaseUtils.FontLoader.GetFont(fnt.FontFamily.Name, fnt.Size - 0.5f, fnt.Style);
            ownfont = true;
        }
    }

    // this scales the font up or down to fit width and height.  Text is not allowed to wrap, its unformatted
    // fnt itself is not deallocated.
    public static Font GetFontToFit(string text, Font fnt, Size areasize)
    {
        if (!text.HasChars())       // can't tell
            return fnt;

        SizeF drawnsize = BaseUtils.BitMapHelpers.MeasureStringUnformattedLengthInBitmap(text, fnt);

        bool smallerthanbox = Math.Ceiling(drawnsize.Width) <= areasize.Width && Math.Ceiling(drawnsize.Height) < areasize.Height;
        float dir = smallerthanbox ? 0.5f : -0.5f;
        float fontsize = fnt.Size;
        System.Diagnostics.Debug.WriteLine($"Autofont {fnt.Name} {fnt.Size} fit {areasize} = {drawnsize} {smallerthanbox} dir {dir}");

        bool ownfont = false;

        while (true)
        {
            fontsize += dir;

            Font fnt2 = BaseUtils.FontLoader.GetFont(fnt.FontFamily.Name, fontsize, fnt.Style);

            drawnsize = BaseUtils.BitMapHelpers.MeasureStringUnformattedLengthInBitmap(text, fnt2);
            smallerthanbox = Math.Ceiling(drawnsize.Width) <= areasize.Width && Math.Ceiling(drawnsize.Height) < areasize.Height;

            System.Diagnostics.Debug.WriteLine($"Autofontnext  {fnt2.Name} {fnt2.Size} fit {areasize} = {drawnsize} {smallerthanbox} dir {dir}");

            // conditions to stop, betting too big, betting small enough, too small font
            if ((dir > 0 && !smallerthanbox) || (dir < 0 && smallerthanbox) || (dir < 0 && fnt.Size < 2))
            {
                fnt2.Dispose();
                return fnt;
            }
            else
            {
                if (ownfont)
                    fnt.Dispose();
                fnt = fnt2;
                ownfont = true;
            }
        }
    }




    public static Size MeasureItems(this Graphics g, Font fnt, string[] array, StringFormat fmt)
    {
        Size max = new Size(0, 0);
        foreach (string s in array)
        {
            SizeF f = g.MeasureString(s, fnt, new Point(0, 0), fmt);
            max = new Size(Math.Max(max.Width, (int)(f.Width + 0.99)), Math.Max(max.Height, (int)(f.Height + 0.99)));
        }

        return max;
    }

    public static int ScalePixels(this Font f, int nominalat12)      //given a font, and size at normal 12 point, what size should i make it now
    {
        return (int)(f.GetHeight() / 18 * nominalat12);
    }

    public static float ScaleSize(this Font f, float nominalat12)      //given a font, and size at normal 12 point, what size should i make it now
    {
        return f.GetHeight() / 18 * nominalat12;
    }

    // used to compute ImageAttributes, given a disabled scaling, a remap table, and a optional color matrix
    static public void ComputeDrawnPanel(out ImageAttributes Enabled,
                    out ImageAttributes Disabled,
                    float disabledscaling, System.Drawing.Imaging.ColorMap[] remap, float[][] colormatrix = null)
    {
        Enabled = new ImageAttributes();
        Enabled.SetRemapTable(remap, ColorAdjustType.Bitmap);
        if (colormatrix != null)
            Enabled.SetColorMatrix(new ColorMatrix(colormatrix), ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

        Disabled = new ImageAttributes();
        Disabled.SetRemapTable(remap, ColorAdjustType.Bitmap);

        if (colormatrix != null)
        {
            colormatrix[0][0] *= disabledscaling;     // the identity positions are scaled by BDS 
            colormatrix[1][1] *= disabledscaling;
            colormatrix[2][2] *= disabledscaling;
            Disabled.SetColorMatrix(new ColorMatrix(colormatrix), ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        }
        else
        {
            float[][] disabledMatrix = {
                        new float[] {disabledscaling,  0,  0,  0, 0},        // red scaling factor of BDS
                        new float[] {0,  disabledscaling,  0,  0, 0},        // green scaling factor of BDS
                        new float[] {0,  0,  disabledscaling,  0, 0},        // blue scaling factor of BDS
                        new float[] {0,  0,  0,  1, 0},        // alpha scaling factor of 1
                        new float[] {0,0,0, 0, 1}};    // three translations of 0

            Disabled.SetColorMatrix(new ColorMatrix(disabledMatrix), ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        }
    }

    #endregion
}
