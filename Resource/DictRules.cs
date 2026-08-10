using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public static class DictRules
{
    //Keyword Formatting Rules
    public static string[] commentSingleRow = { "//" };
    public static string[] grammar = { "{", "}", "{}", "(", ")", "()", ".", ",", ";" };
    public static string[] number = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "True", "False" };
    public static string[] dataType = { "int", "float", "char", "bool" };
    public static string[] dataTypeComplex = { "void", "string", "int[]", "float[]", "char[]", "bool[]" };
    public static string[] baseOperator = { "<<", ">>" };
    public static string[] mathOperator = { "+", "-", "*", "/", "^", "^/", "++", "--", "++()", "--()" };
    public static string[] logicOperator = { "<", "<=", ">", ">=", "==", "!!", ">><<", "<<>>" };
    public static string[] booleanOperator = { "!", "&", "||", "|&|" };
    public static string[] functionOperator = { "ret", "brk", "end", "out" };
    public static string[] flushOperator = { "if", "while", "do", "for" };

    //Block Formatting Rules    
    public static string commentStart = "/*";
    public static string commentEnd = "*/";
    public static string libraryStart = "@@'";
    public static string libraryEnd = "'";
    public static string stringStart = "\"";
    public static string stringEnd = "\"";

    //Color Formatting Rules
    public static Color libraryColor = Color.Orange;
    public static Color commentColor = Color.Green;
    public static Color stringColor = Color.Coral;
    public static Color grammarColor = Color.Yellow;  
    public static Color numberColor = Color.Cyan;
    public static Color dataTypeColor = Color.LightGreen;
    public static Color dataTypeComplexColor = Color.LightGreen;
    public static Color baseOperatorColor = Color.Pink;
    public static Color mathOperatorColor = Color.Pink;
    public static Color logicOperatorColor = Color.Pink;
    public static Color booleanOperatorColor = Color.Pink;
    public static Color functionOperatorColor = Color.GreenYellow;
    public static Color flushOperatorColor = Color.HotPink;


}

