﻿/*
 * Copyright © 2018 EDDiscovery development team
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
using System.Linq;

namespace BaseUtils
{
    public class Eval : IEval
    {
        public Eval(bool checkend = false, bool allowfp = false, bool allowstrings = false, bool allowmembers = false, bool allowarrays = false)
        {
            sp = null;
            CheckEnd = checkend;
            AllowFP = allowfp;
            AllowStrings = allowstrings;
            AllowMemberSymbol = allowmembers;
            AllowArrays = allowarrays;
        }

        public Eval(string s, bool checkend = false, bool allowfp = false, bool allowstrings = false, bool allowmembers = false, bool allowarrays = false) : this(checkend, allowfp, allowstrings, allowmembers, allowarrays)
        {
            sp = new StringParser(s);
        }

        public Eval(StringParser parse, bool checkend = false, bool allowfp = false, bool allowstrings = false, bool allowmembers = false, bool allowarrays = false) : this(checkend, allowfp, allowstrings, allowmembers, allowarrays)
        {
            sp = parse;
        }

        public IEval Clone()     // clone with options, but without parser..
        {
            return new Eval(CheckEnd, AllowFP, AllowStrings)
            { DefaultBase = this.DefaultBase, ReplaceEscape = this.ReplaceEscape, IgnoreCase = this.IgnoreCase, Culture = this.Culture,
                ReturnSymbolValue = this.ReturnSymbolValue, SetSymbolValue = this.SetSymbolValue, ReturnFunctionValue = this.ReturnFunctionValue };
        }

        public int DefaultBase { get; set; } = 10;              // default base value
        public bool CheckEnd { get; set; } = false;             // after expression, check string is at end
        public bool ReplaceEscape { get; set; } = false;        // in strings, expand escape
        public bool AllowFP { get; set; } = false;              // Allow floating point values
        public bool AllowStrings { get; set; } = false;         // Allow strings
        public bool UnaryEntry { get; set; } = false;           // enter at unary level, requires () to do other operators
        public bool IgnoreCase { get; set; } = false;           // ignore case on string checks
        public bool AllowMemberSymbol { get; set; } = false;    // allow Rings.member syntax on symbols
        public bool AllowArrays { get; set; } = false;     // allow Rings[n] syntax on symbols
        public bool Fake { get; set; } = false;                 // set to do a Fake eval - all data is set to 1L. Errors are ignored. Useful for extracting symbols used by hooking into ReturnSymbolValue

        public System.Globalization.CultureInfo Culture { get; set; } = System.Globalization.CultureInfo.InvariantCulture;

        public StringParser Parser { get { return sp; } }       // get parser, can use after use to get rest of string
        public bool InError { get { return value is StringParser.ConvertError; } }  // if in error
        public Object Value { get { return value; } }           // current value

        public Func<string, Object> ReturnSymbolValue;          // if set, evaluate and return value, or return ConvertError
        public Func<string, Object, Object> SetSymbolValue;     // if set, set symbol value, return value, or return ConvertError 
        public Func<string, IEval, Object> ReturnFunctionValue; // if set, evaluate and return value, or return ConvertError

        #region Public IF

        public Object Evaluate(string s)     // return StringParser.ConvertError, string, double, long
        {
            sp = new StringParser(s);
            return Evaluate(UnaryEntry, CheckEnd);
        }

        public Object EvaluateQuickCheck(string s)     // return StringParser.ConvertError, string, double, long
        {
            if ( double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double resd))
            {
                if (long.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long resl))
                {
                    value = resl;       // remember to update value in case its used, and to make sure any previous error is cancelled.
                    return resl;
                }
                else
                {
                    value = resd;
                    return resd;
                }
            }
            sp = new StringParser(s);
            return Evaluate(UnaryEntry, CheckEnd);
        }

        public Object Evaluate()     // return StringParser.ConvertError, string, double, long
        {
            Evaluate(UnaryEntry, CheckEnd);
            return value;
        }

        public Object Evaluate(string s, bool unary, bool checkend)     // return StringParser.ConvertError, string, double, long
        {
            sp = new StringParser(s);
            return Evaluate(unary,checkend);
        }

        public Object Evaluate(bool unary, bool checkend)          // Allow control of unary entry and check end on a case by case basis
        {
            System.Diagnostics.Debug.Assert(sp != null);

            unaryentrytimes = 0;

            if ( unary )
                Level0Unary();
            else
                Level12Assign();

            if (!InError && checkend && !sp.IsEOL)
                value = new StringParser.ConvertError("Extra characters after expression: " + sp.LineLeft);

            //System.Diagnostics.Debug.WriteLine("Evaluate Value is " + value + " : of type " + value.GetType().Name);
            return value;
        }

        public Object EvaluateDouble(bool unary, bool checkend)          // Allow control of unary entry and check end on a case by case basis
        {
            Evaluate(unary, checkend);

            if (value is long)
                value = (double)(long)value;

            if (InError || value is double)
                return value;
            else
                return new StringParser.ConvertError("Expression must evaluate to a number");
        }

        public Object EvaluateLong(bool unary, bool checkend)          // Allow control of unary entry and check end on a case by case basis
        {
            Evaluate(unary, checkend);

            if (InError || value is long)
                return value;
            else
                return new StringParser.ConvertError("Expression must evaluate to an integer");
        }

        // for supporting functions..  given a list of types of parameters, collect them, comma separ.
        public List<Object> Parameters(string nameforerrorreport, int minparas, IEvalParaListType [] paratypes)
        {
            List<Object> list = new List<object>();

            for (int n = 0; n < paratypes.Length; n++)
            {
                Object evres = Evaluate(false, false);
                if (InError)
                    return null;

                if ((paratypes[n] == IEvalParaListType.String && (evres is string)) ||
                    (paratypes[n] == IEvalParaListType.Number && (evres is double || evres is long)) ||
                    (paratypes[n] == IEvalParaListType.NumberOrInteger && (evres is double || evres is long)) ||
                    (paratypes[n] == IEvalParaListType.Integer && (evres is long)) ||
                    (paratypes[n] == IEvalParaListType.IntegerOrString && (evres is string || evres is long)) ||
                    (paratypes[n] == IEvalParaListType.All)
                    )
                {
                    if (paratypes[n] is IEvalParaListType.Number && evres is long)
                        evres = (double)(long)evres;

                    list.Add(evres);

                    if (n < paratypes.Length - 1)    // if not maximum point
                    {
                        if (!sp.IsCharMoveOn(','))  // and not comma..
                        {
                            if (list.Count() < minparas) // ensure minimum
                            {
                                value = new StringParser.ConvertError(nameforerrorreport + "() requires " + minparas + " parameters minimum");
                                return null;
                            }
                            else
                                return list;        // have min, return it
                        }
                    }
                }
                else
                {
                    value = new StringParser.ConvertError(nameforerrorreport + "() type mismatch in parameter " + (n + 1) + " wanted " + paratypes[n].ToString().SplitCapsWord());
                    return null;
                }
            }

            if (sp.IsChar(','))     // if at max, can't have another
            {
                value = new StringParser.ConvertError(nameforerrorreport + "() takes " + paratypes.Length + " parameters maximum");
                return null;
            }

            return list;
        }

        public string ToString(System.Globalization.CultureInfo ci)
        {
            if (InError)
                return ((StringParser.ConvertError)value).ErrorValue;
            else if (value is double)
                return ((double)value).ToString(ci);
            else if (value is long)
                return ((long)value).ToString(ci);
            else
                return (string)value;
        }

        public bool ToSafeString(string fmt, out string ret)
        {
            if (InError)
            {
                ret = ((StringParser.ConvertError)value).ErrorValue;
                return false;
            }
            else if (value is double)
            {
                return ((double)value).SafeToString(fmt, out ret);
            }
            else if (value is long)
                return ((long)value).SafeToString(fmt, out ret);
            else
            {
                ret = (string)value;
                return true;
            }
        }

        #endregion

        #region Static Helpers

        // return StringParser.ConvertError, string, double, long
        static public Object Expr(string s, bool checkend = true, bool allowfp = true, bool allowstrings = true) 
        {
            Eval v = new Eval(s);
            v.CheckEnd = checkend;
            v.AllowFP = allowfp;
            v.AllowStrings = allowstrings;
            return v.Evaluate();
        }

        #endregion

        #region Level Evaluators

        private void Level0Unary()      // Value left as Error, double, long, string
        {
            unaryentrytimes++;          // counts entry to the unary level.. if 1, and symbolname set, its a symbol def on left side 
            lvaluename = null;

            long sign = 0;

            if (sp.IsCharMoveOn('-'))   // space allowed after unary minus plus to entry
                sign = -1;
            else if (sp.IsCharMoveOn('+'))
                sign = 1;

            if (sp.IsCharMoveOn('('))       // ( value )
            {
                value = Evaluate(false, false);

                if (!(value is StringParser.ConvertError) && !sp.IsCharMoveOn(')'))
                {
                    value = new StringParser.ConvertError("Missing ) at end of expression");
                }
            }
            else
            {
                value = sp.ConvertNumberStringSymbolChar(DefaultBase, AllowFP, AllowStrings, ReplaceEscape, AllowMemberSymbol);

                if (value is StringParser.ConvertSymbol)    // symbol must resolve to a value or Error
                {
                    string symname = (value as StringParser.ConvertSymbol).SymbolValue;

                    if (sp.IsCharMoveOn('('))
                    {
                        if (ReturnFunctionValue != null)
                        {
                            value = ReturnFunctionValue(symname, this);

                            if (!(value is StringParser.ConvertError) && !sp.IsCharMoveOn(')'))         // must be ) terminated
                                value = new StringParser.ConvertError("Function not terminated with )");
                        }
                        else
                            value = new StringParser.ConvertError("Functions not supported");
                    }
                    else if (ReturnSymbolValue == null)
                    {
                        value = new StringParser.ConvertError("Symbols not supported");
                    }
                    else 
                    {
                        while (AllowArrays && sp.IsCharMoveOn('['))            // is it an array symbol..
                        {
                            value = Evaluate(false, false);     // get [] expression

                            if (value is StringParser.ConvertError)     // see what we have back and generate array
                                break;
                            if (value is long)
                                symname += $"[{((long)value).ToStringInvariant()}]";
                            else if (value is double)
                            {
                                value = new StringParser.ConvertError("Cannot use floating point value as array index");
                                break;
                            }
                            else
                                symname += $"[{((string)value).AlwaysQuoteString()}]";

                            if (!sp.IsCharMoveOn(']', false))         // must be ] terminated
                            {
                                value = new StringParser.ConvertError("Array not terminated with ]");
                                break;
                            }

                            if (!sp.IsLetterDigitUnderscoreMember())        // if no following chars, we are done 
                                break;

                            string moresym;         // more symbol text beyond it

                            if (AllowMemberSymbol)
                                moresym = sp.NextWord((c) => { return char.IsLetterOrDigit(c) || c == '_' || c == '.'; });
                            else
                                moresym = sp.NextWord((c) => { return char.IsLetterOrDigit(c) || c == '_'; });

                            symname += moresym;     // add on
                        }

                        if (!(value is StringParser.ConvertError))      // if not in error, see if symbols is there
                        {
                            lvaluename = (sign == 0) ? symname : null;              // pass back symbol name found, only if not signed.
                            value = ReturnSymbolValue(symname);                     // could be Error with symbol value in it.
                        }
                    }
                }
            }

            // value now Error, double, long, string

            if (!(value is StringParser.ConvertError) && sign != 0)      // if not error, and signed
            {
                if (value is double)
                    value = (double)value * sign;
                else if (value is long)
                    value = (long)value * sign;
                else
                    value = new StringParser.ConvertError("Unary +/- not allowed in front of string");
            }

            if (Fake)       // if we are faking the eval, return 1L long as the default output.
                value = 1L;
        }

        private void Level1Not()
        {
            if (sp.IsCharMoveOn('!'))
            {
                Level1Not();    // allow recurse..

                lvaluename = null;      // clear symbol name, can't use with !

                if (IsLong)
                    value = ((long)value != 0) ? 0L : 1L;
                else if (!InError)
                    value = new StringParser.ConvertError("! only valid with integers");
            }
            else if (sp.IsCharMoveOn('~'))
            {
                Level1Not();    // allow recurse..

                lvaluename = null;      // clear symbol name, can't use with ~

                if (IsLong)
                    value = ~(long)value;
                else if (!InError)
                    value = new StringParser.ConvertError("~ is only valid with integers");
            }
            else
                Level0Unary();
        }

        private void Level2Times()
        {
            Level1Not();        // get left value

            while (!InError && sp.IsCharOneOf("*/%"))
            {
                if (!IsNumeric)
                {
                    value = new StringParser.ConvertError("*/% only valid with numbers");
                    return;
                }

                char operation = sp.GetChar(skipspace: true);

                Object leftside = value;            // remember left side value

                Level1Not();                        // get right side

                if (InError)
                    return;
                else if ( !IsNumeric )
                {
                    value = new StringParser.ConvertError("*/% only valid with numbers");
                    return;
                }

                if (leftside is double || value is double)    // either double, its double
                {
                    double left = AsDouble(leftside);
                    double right = AsDouble(value);

                    if (operation == '*')
                        value = left * right;
                    else if (operation == '/')
                    {
                        if (right == 0)
                        {
                            value = new StringParser.ConvertError("Divide by zero");
                            return;
                        }

                        value = left / right;
                    }
                    else
                    {
                        value = new StringParser.ConvertError("Cannot perform modulo with floating point values");
                        return;
                    }
                }
                else
                {
                    long left = (long)(leftside);
                    long right = (long)(value);

                    if (operation == '*')
                        value = left * right;
                    else if (operation == '/')
                    {
                        if (right == 0)
                        {
                            value = new StringParser.ConvertError("Divide by zero");
                            return;
                        }

                        value = left / right;
                    }
                    else
                    {
                        value = left % right;
                    }
                }
            }
        }

        private void Level3Add()
        {
            Level2Times();        // get left value

            while (!InError && sp.IsCharOneOf("+-"))
            {
                char operation = sp.GetChar(skipspace: true);

                if (IsString && operation == '-')
                {
                    value = new StringParser.ConvertError("- is not supported with strings");
                    return;
                }

                Object leftside = value;            // remember left side value

                Level2Times();                      // get right side

                if (InError)
                    return;

                if (leftside is string && IsString) // two strings, +
                {
                    value = (leftside as string) + (value as string);
                }
                else
                {
                    if (leftside is string || IsString)
                    {
                        value = new StringParser.ConvertError("Cannot mix string and number types");
                        return;
                    }
                    else if (leftside is double || value is double)    // either double, its double
                    {
                        double left = AsDouble(leftside);
                        double right = AsDouble(value);

                        if (operation == '+')
                            value = left + right;
                        else
                            value = left - right;
                    }
                    else
                    {
                        long left = (long)(leftside);
                        long right = (long)(value);

                        if (operation == '+')
                            value = left + right;
                        else
                            value = left - right;
                    }
                }
            }
        }

        private void Level4Shift()
        {
            Level3Add();        // get left value

            bool leftshift = false;

            while (!InError && ((leftshift = sp.IsString("<<")) || sp.IsString(">>")))
            {
                sp.MoveOn(2);

                if (CheckLong("<< and >>"))
                    return;

                Object leftside = value;            // remember left side value

                Level3Add();        // get right side

                if (InError || CheckLong("<< and >>", true))
                    return;

                if (leftshift)
                    value = (long)leftside << (int)(long)value;
                else
                    value = (long)leftside >> (int)(long)value;
            }
        }

        private void Level5GreaterLess()
        {
            Level4Shift();        // get left value

            while (!InError && sp.IsCharOneOf("<>"))
            {
                bool left = sp.GetChar() == '<';
                bool equals = sp.IsCharMoveOn('=');
                sp.SkipSpace();

                Object leftside = value;            // remember left side value

                Level4Shift();        // get right side

                if (InError)
                    return;

                bool result;

                if (leftside is string || value is string)
                {
                    if ( !(leftside is string ) || !(value is string))
                    {
                        value = new StringParser.ConvertError("Cannot mix string and number types in comparisions");
                        return;
                    }
                    else
                    {
                        int cmp = String.Compare(leftside as string, value as string, IgnoreCase, Culture);
                        if (left)
                            result = equals ? (cmp <= 0) : (cmp < 0);
                        else
                            result = equals ? (cmp >= 0) : (cmp > 0);
                    }
                }
                else if (leftside is double || value is double)
                {
                    double l = AsDouble(leftside);
                    double r = AsDouble(value);

                    if (left)
                        result = equals ? (l <= r) : (l < r);
                    else
                        result = equals ? (l >= r) : (l > r);
                }
                else
                {
                    long l = (long)(leftside);
                    long r = (long)(value);

                    if (left)
                        result = equals ? (l <= r) : (l < r);
                    else
                        result = equals ? (l >= r) : (l > r);
                }

                value = result ? (long)1 : (long)0;
            }
        }

        private void Level6Equals()
        {
            Level5GreaterLess();        // get left value

            bool equals = false;

            while (!InError && ((equals = sp.IsString("==")) || sp.IsString("!="))) 
            {
                sp.MoveOn(2);

                Object leftside = value;            // remember left side value

                Level5GreaterLess();        // get left value

                if (InError)
                    return;

                bool result;

                if (leftside is string || value is string)
                {
                    if (!(leftside is string) || !(value is string))
                    {
                        value = new StringParser.ConvertError("Cannot mix string and number types in comparisions");
                        return;
                    }
                    else
                    {
                        int cmp = String.Compare(leftside as string, value as string, IgnoreCase, Culture);
                        result = equals ? (cmp == 0) : (cmp != 0);
                    }
                }
                else if (leftside is double || value is double)
                {
                    double l = AsDouble(leftside);
                    double r = AsDouble(value);
                    result = equals ? (l == r) : (l != r);
                }
                else
                {
                    long l = (long)(leftside);
                    long r = (long)(value);
                    result = equals ? (l == r) : (l != r);
                }

                value = result ? (long)1 : (long)0;
            }
        }

        private void Level7BinaryAnd()
        {
            Level6Equals();        // get left value

            while (!InError && (sp.IsChar('&') && !sp.IsNextChar('&')))     // & not &&
            {
                sp.MoveOn(1);

                if (CheckLong("&"))
                    return;

                Object leftside = value;            // remember left side value

                Level6Equals();        // get left value

                if (InError || CheckLong("&", true))
                    return;

                value = (long)((long)leftside & (long)value);
            }
        }

        private void Level8BinaryEor()
        {
            Level7BinaryAnd();        // get left value

            while (!InError && sp.IsChar('^'))     // ^
            {
                sp.MoveOn(1);

                if (CheckLong("^"))
                    return;

                Object leftside = value;            // remember left side value

                Level7BinaryAnd();        // get left value

                if (InError || CheckLong("^", true))
                    return;

                value = (long)((long)leftside ^ (long)value);
            }
        }

        private void Level9BinaryOr()
        {
            Level8BinaryEor();        // get left value

            while (!InError && (sp.IsChar('|') && !sp.IsNextChar('|')))     // | not ||
            {
                sp.MoveOn(1);

                if (CheckLong("|"))
                    return;

                Object leftside = value;            // remember left side value

                Level8BinaryEor();        // get left value

                if (InError || CheckLong("|", true))
                    return;

                value = (long)((long)leftside | (long)value);
            }
        }

        private void Level10And()
        {
            Level9BinaryOr();        // get left value

            while (!InError && sp.IsStringMoveOn("&&"))
            {
                if (CheckLong("&&"))
                    return;

                Object leftside = value;            // remember left side value

                Level9BinaryOr();        // get left value

                if (InError || CheckLong("&&", true))
                    return;

                bool l = (long)leftside != 0;
                bool r = (long)value != 0;
                value = (l && r) ? (long)1 : (long)0;
            }
        }

        private void Level11Or()
        {
            Level10And();        // get left value

            while (!InError && sp.IsStringMoveOn("||"))
            {
                if (CheckLong("||"))
                    return;

                Object leftside = value;            // remember left side value

                Level10And();        // get left value

                if (InError || CheckLong("||", true))
                    return;

                bool l = (long)leftside != 0;
                bool r = (long)value != 0;
                value = (l || r) ? (long)1 : (long)0;
            }
        }

        private void Level12Assign()
        {
            Level11Or();        // get left value

            if (sp.IsCharMoveOn('='))
            {
                if (SetSymbolValue == null)
                {
                    value = new StringParser.ConvertError("= operator not supported");
                }
                else
                {
                    StringParser.ConvertError err = value as StringParser.ConvertError;

                    string symbolname;

                    if (unaryentrytimes == 1 && lvaluename != null)    // if 1 entry to unary, and symbol value is set, its a current value
                        symbolname = lvaluename;
                    else
                    {                                                   // else its an equal without an error but not a symbol, so its not an lvalue
                        value = new StringParser.ConvertError("Lvalue required for = operator");
                        return;
                    }

                    Evaluate(false, false);                             // get next expression

                    if (!InError)
                        value = SetSymbolValue(symbolname, value);
                }
            }
        }

        #endregion


        #region Helpers and privates

        private StringParser sp;        // string parser
        private string lvaluename;      // symbol saw at unary level..
        private int unaryentrytimes;        // no of times at unary
        private Object value = null;        // this can be Error class, double, long, string

        private bool IsNumeric { get { return value is double || value is long; } }
        private bool IsLong { get { return value is long; } }
        private bool IsDouble { get { return value is double; } }
        private bool IsString { get { return value is string; } }

        private double AsDouble(Object v) { if (v is double) return (double)v; else return (double)(long)v; } // must be numeric before call

        private bool CheckLong(string op, bool right = false)
        {
            if (!IsLong)
            {
                value = new StringParser.ConvertError(op + " requires integer on " + ((right) ? "right" : "left") + " side");
                return true;
            }
            else
                return false;
        }

        // set up the evaluator for checking vars
        public static HashSet<string> VarsInUse(Action<Eval> check, bool checkend = true, bool allowfp = true, bool allowstrings = true, 
                                                bool allowmembers = true, bool allowarrays = true)
        {
            HashSet<string> str = new HashSet<string>();
            Eval evl = new Eval(checkend,allowfp,allowstrings,allowmembers,allowarrays);
            evl.Fake = true;
            evl.ReturnFunctionValue = BaseFunctionsForEval.BaseFunctions;
            evl.ReturnSymbolValue += (string s) =>
            {
                str.Add(s);
                //System.Diagnostics.Debug.WriteLine($"Sym {s}");
                return 1L;
            };

            check.Invoke(evl);

            return str;
        }

        #endregion
    }
}
