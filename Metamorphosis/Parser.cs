﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metamorphosis
{
    enum ElementType
    {
        None,
        Import,
        Method,
        Type,
        OutputLanguage,
        DefinitionFile,

        ImportInclude,

        Struct,
        StructDefinition,
        StructDefinitionWithBase,
        StructFieldDeclaration,

        Static,
        StaticDefinition,
        StaticFieldDeclaration,
        StaticFieldDeclarationWithValue,
        StaticFieldDefinition,
        StaticFieldDefinitionWithValue,

        Enum,
        EnumDefinition,
        EnumFieldDeclaration,
        EnumFieldWithValueDeclaration,
        EnumValueDeclaration,

        MethodDeclaration,
        MethodDefinition,

        StaticMethodDeclaration,
        StaticMethodDefinition,

        IncludeDeclarationTop,
        IncludeDefinitionTop,
        UserIncludeDeclarationTop,
        Namespace,

        ConstructorDeclaration,
        ConstructorWithBaseDeclaration,
        ConstructorDefinition,
        ConstructorWithBaseDefinition,

        FieldInitialisation,

        OutputName,

        Error,
        EOF,
    };

    enum LarvaMode
    {
        Generate,
        Skip,
    };

    enum ParserMode
    {
        Unknown,
        Normal,
        ImportRules,
        ImportLarva,
    };

    class Parser
    {
        static string[] enumNames = Enum.GetNames(typeof(ElementType));
        List<string> tokens = new List<string>();
        char[] seperators = new char[] { ' ', '\n', '\r', '\t' };
        string[] seperatorTokens = new string[] { "+=", "==", "!{", "!}", "=", ";", "," };
        int currentToken;
        ParserMode Mode;
        object element;

        public static ElementType ConvertToElementType(string text)
        {
            ElementType t = ElementType.None;
            // check is it global variable
            if (enumNames.Contains(text))
            {
                t = (ElementType)Enum.Parse(typeof(ElementType), text, true);                
            }

            return t;
        }

        public void AddToken(string text, int from, int to, bool seperate)
        {
            if (seperate)
            {
                SeperateToken(text.Substring(from, to - from));
            }
            else
            {
                tokens.Add(text.Substring(from, to - from));
            }
        }

        public void SeperateToken(string token)
        {
            // search for all token seperators
            foreach (string st in seperatorTokens)
            {
                while (true)
                {
                    int i = token.IndexOf(st);

                    if (i == -1)
                    {
                        break;
                    }

                    if (i > 0)
                    {
                        // add token from the left of seperator
                        tokens.Add(token.Substring(0, i));
                    }
                    tokens.Add(st);
                    token = token.Remove(0, i + st.Length);
                }
            }

            if (token != "")
            {
                tokens.Add(token);
            }

        }

        string RemoveComment(string text)
        {
            while (true)
            {
                int i = text.IndexOf(@"##");
                if (i == -1)
                {
                    break;
                }
                int end = text.IndexOf(Environment.NewLine, i);

                if (end == -1)
                {
                    // delete till end of string
                    text = text.Substring(0, i);
                }
                else
                {
                    end += Environment.NewLine.Length;
                    text = text.Remove(i, end - i);
                }
            }

            return text;
        }

        public Parser(string text, ParserMode mode = ParserMode.Normal)
        {
            text = RemoveComment(text);
            Mode = mode;
            currentToken = 0;

            int cp = 0;

            int ib = text.IndexOf("!{");

            if (ib == -1)
            {
                ib = text.Length + 1;
            }

            while (cp < text.Length)
            {
                // find token, skip seperators
                while (cp < text.Length && seperators.Contains(text[cp]))
                {
                    cp++;
                }

                int tb = cp;

                if (tb == ib)
                {
                    // start long token from !{
                    tb += 2;
                    int ie = text.IndexOf("!}", tb);
                    if (tb == -1)
                    {
                        Console.WriteLine("Parser error: can't find !}");
                        break;
                    }
                    AddToken(text, tb, ie, false);

                    cp = ie + 2;

                    ib = text.IndexOf("!{", cp);

                    if (ib == -1)
                    {
                        ib = text.Length + 1;
                    }
                    continue;
                }

                // find token end, find seperator
                while (cp < text.Length && !seperators.Contains(text[cp]))
                {
                    cp++;
                }

                int te = cp;

                if (tb < te)
                {
                    if (te > ib)
                    {
                        te = ib;
                    }

                    AddToken(text, tb, te, true);

                    cp = te;
                }
            }
        }

        bool IsNextToken(string token, int step = 0)
        {
            int i = currentToken + step;
            if (i >= tokens.Count)
            {
                return false;
            }

            return tokens[i] == token;
        }

        string GetNextToken()
        {
            if (tokens.Count == currentToken)
            {
                return null;
            }
            else
            {
                return tokens[currentToken++];
            }
        }

        int GetNextTokenIndex(string token)
        {
            for (int i = currentToken; i < tokens.Count; i++)
            {
                if (token == tokens[i])
                {
                    return i;
                }
            }

            return -1;
        }

        public ElementType Next()
        {
            element = null;

            string token = GetNextToken();

            if (token == null)
            {
                return ElementType.EOF;
            }

            // check is it global variable
            ElementType et = ConvertToElementType(token);
            if (et != ElementType.None)
            {                 
                string operation = GetNextToken();
                string value = GetNextToken();

                if (Mode == ParserMode.Normal || Mode == ParserMode.ImportRules)
                {
                    switch (operation)
                    {
                        case "=":
                            Larvae.SetElement(et, value);
                            break;

                        case "+=":
                            value = Larvae.GetElement(et) + value;
                            Larvae.SetElement(et, value);
                            break;

                        default:
                            Log.Error(operation, Error.UnexpectedToken, " expected for \"=\" or \"+=\"");
                            break;
                    }
                }
                
                element = value;
                return et;
            }

            switch (token)
            {
                case "static":
                case "struct":
                {
                    LarvaType larvaType = token == "struct" ? LarvaType.Struct : LarvaType.Static;
                    // parse struct data
                    string larvaName = GetNextToken();
                    string fullName = Larvae.GetFullName(larvaName);
                    Larva larva = Larvae.GetLarva(fullName);

                    larva.Name = larvaName;
                    larva.Namespace = Larvae.GetElement(ElementType.Namespace);
                    larva.TypeInfo = larvaType == LarvaType.Struct ? TypeFactory.Get("struct", true) : TypeFactory.Get("static", true);

                    switch (Mode)
                    {
                        case ParserMode.Normal:
                        case ParserMode.ImportLarva:
                            larva.Mode = LarvaMode.Generate;
                            break;

                        default:
                            larva.Mode = LarvaMode.Skip;
                            break;
                    }

                    larva.Type = larvaType;
                    token = GetNextToken();

                    if (token == ":")
                    {
                        token = GetNextToken();
                        larva.BaseName = token;
                        token = GetNextToken();

                        if (token == "base")
                        {
                            token = GetNextToken();
                            // read base field initialisation
                            if (token != "{")
                            {
                                Log.Error(token, Error.UnexpectedToken, "Expected symbol '{' after base " + larva.Name + " defenition");
                                return ElementType.Error;
                            }

                            token = GetNextToken();
                            while (token != "}")
                            {
                                Part p = new Part(larva);
                                larva.BaseParts.Add(p);

                                p.Name = token;

                                token = GetNextToken();                                
                                if (token != "=")
                                {
                                    Console.WriteLine("Error: Expected symbol '=' in base " + larva.Name + " defenition");
                                    return ElementType.Error;
                                }

                                // initial value
                                token = GetNextToken();
                                p.InitialValue = token;

                                // must be ended with , or }
                                token = GetNextToken();                                
                                if (token != "," && token != "}")
                                {
                                    Console.WriteLine("Error: Expected symbol ',' in base " + larva.Name + " defenition");
                                    return ElementType.Error;
                                }

                                // skip ,
                                if (token == ",")
                                {
                                    token = GetNextToken();
                                }
                            }

                            token = GetNextToken();
                        }
                    }

                    if (token != "{")
                    {
                        Log.Error(token, Error.UnexpectedToken, "Expected symbol '{' after struct " + larva.Name + " defenition");
                        return ElementType.Error;
                    }

                    int ie = GetNextTokenIndex("}");

                    if (ie == -1)
                    {
                        Log.Error("}", Error.NotFound, "struct closing symbol '}' not found");
                        return ElementType.Error;
                    }

                    while (currentToken < ie)
                    {
                        Part p = new Part(larva);

                        Larva l = GetLarva();
                        string t = GetNextToken();

                        p.Larva = l;
                        p.Name = t;

                        t = GetNextToken();

                        if (t == "=")
                        {
                            p.InitialValue = GetNextToken();
                            t = GetNextToken();
                        }

                        if (t ==":")
                        {
                            t = GetNextToken(); // read modificator
                            if (t == "skip")
                            {
                                p.Mode = PartMode.Skip;
                            }
                            t = GetNextToken();
                        }

                        if (t != ";")
                        {
                            Log.Error(token, Error.UnexpectedToken, "Unexpected token '" + token +"' field name not found");
                            return ElementType.Error;
                        }

                        larva.Parts.Add(p);
                    }

                    GetNextToken(); // read !}

                    // check for methods name that we will need to generate
                    if (IsNextToken(":"))
                    {
                        token = GetNextToken(); // read :
                        while (true)
                        {
                            token = GetNextToken();
                            if (token == ";")
                            {
                                break;
                            }
                            larva.Methods.Add(token);
                        }
                    }

                    element = larva;
                    return ElementType.Struct;
                }

                case "enum":
                {
                    // parse struct data
                    string larvaName = GetNextToken();
                    string fullName = Larvae.GetFullName(larvaName);
                    Larva larva = Larvae.GetLarva(fullName);

                    larva.Name = larvaName;
                    larva.Namespace = Larvae.GetElement(ElementType.Namespace);
                    larva.TypeInfo = TypeFactory.Get("enum", true);

                    switch (Mode)
                    {
                        case ParserMode.Normal:
                        case ParserMode.ImportLarva:
                            larva.Mode = LarvaMode.Generate;
                            break;

                        default:
                            larva.Mode = LarvaMode.Skip;
                            break;
                    }

                    larva.Type = LarvaType.Enum;
                    token = GetNextToken();

                    if (token != "{")
                    {
                        Log.Error(token, Error.UnexpectedToken, "Expected symbol '{' after enum " + larva.Name + " defenition");
                        return ElementType.Error;
                    }

                    while (true)
                    {
                        string t = GetNextToken();

                        if (t == "}")
                        {
                            break;
                        }

                        Part p = new Part(larva);
                        p.Name = t;
                        p.Larva = larva;
                        larva.Parts.Add(p);

                        t = GetNextToken();

                        if (t == "=")
                        {
                            p.InitialValue = GetNextToken();
                            t = GetNextToken();
                        }

                        if (t == ":")
                        {
                            p.Description = GetNextToken();
                            t = GetNextToken();
                        }

                        if (t == "}")
                        {
                            break;
                        }

                        if (t != ",")
                        {
                            Log.Error(token, Error.UnexpectedToken, "Unexpected token '" + token + "' field name not found");
                            return ElementType.Error;
                        }

                    }

                    element = larva;

                    return ElementType.Enum;
                }

                case "import":
                {
                    token = GetNextToken();
                    ParserMode parserMode = ParserMode.Unknown;

                    switch (token.ToLower())
                    {
                        case "larva":
                            parserMode = ParserMode.ImportLarva;
                            break;

                        case "rules":
                            parserMode = ParserMode.ImportRules;
                            break;

                        default:
                            Console.WriteLine("Error. Unknown parser mode: " + token);
                            return ElementType.Error;
                            break;
                    }

                    string fileName = GetNextToken();
                    
                    if (parserMode == ParserMode.ImportLarva)
                    {
                        Larvae.AddImport(fileName);
                    }

                    // save current namespace
                    string ns = Larvae.GetElement(ElementType.Namespace);
                    Parser p = new Parser(Program.ReadAllText(fileName), parserMode);
                    p.Parse();
                    // restore namespace
                    Larvae.SetElement(ElementType.Namespace, ns);
                    return ElementType.Import;
                }

                case "type":
                {
                    token = GetNextToken();
                    int length = 0;
                    try
                    {
                        length = Convert.ToInt32(token);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Can't parse type length: " + e.ToString());
                        return ElementType.Error;
                    }
                    string group = GetNextToken();
                    string name = GetNextToken();

                    token = GetNextToken();

                    if (token != "=")
                    {
                        Console.WriteLine("Unexpected token '" + token + "'. Expected '='");
                        return ElementType.Error;                        
                    }

                    string definition = GetNextToken();

                    TypeInfo td = TypeFactory.Get(name);
                    td.Length = length;
                    td.Group = group;
                    td.Definition = definition;

                    return ElementType.Type;
                }

                case "method":
                {
                    string methodName = GetNextToken();

                    Method m = MethodFactory.GetMethod(methodName);

                    VariableList vl = m.Variables;
                    Variable v = null;
                    while (true)
                    {
                        token = GetNextToken();
                        if (token == "=")
                        {
                            break;
                        }
                        v = vl.GetVariable(token);
                        vl = v.Variables;
                    }

                    token = GetNextToken(); // read variable value

                    v.Value = token;

                    return ElementType.Method;
                }
            }

            element = token;
            return ElementType.None;
        }

        public Larva GetLarva()
        {
            Larva l;
            string type = GetNextToken();

            TypeInfo td = TypeFactory.Get(type, true);

            if (td == null)
            {
                if (!type.Contains('.'))
                {
                    type = Larvae.GetFullName(type);
                }

                l = Larvae.GetLarva(type, true);
                if (l == null)
                {
                    Console.WriteLine("Error! Type '" + type + "' not found");
                    return null;
                }

                return l;
            }
            else
            {
                string fullType = type;

                List<Larva> subLarvae = null;

                if (td.Length > 1)
                {
                    subLarvae = new List<Larva>();
                    for (int i = 1; i < td.Length; i++)
                    {
                        Larva sl = GetLarva();
                        fullType += "." + sl.FullName;
                        subLarvae.Add(sl);
                    }
                }

                l = Larvae.GetLarva(fullType);
                l.Type = LarvaType.Custom;
                l.TypeInfo = td;
                l.SubLarvae = subLarvae;
            }

            return l;
        }

        public object GetElement()
        {
            return element;
        }

        public bool Parse()
        {
            while (true)
            {
                ElementType t = Next();
                switch (t)
                {
                    case ElementType.Enum:
                    case ElementType.Struct:
                        //Larva l = GetElement() as Larva;
                        //Larvae.Items[l.Name] = l;
                        break;

                    case ElementType.EOF:
                        return true;

                    case ElementType.None:
                        Log.Error(GetElement().ToString(), Error.UnknownToken, "Unknown input");
                        return false;

                    case ElementType.Error:
                        return false;

                    case ElementType.Import:
                        break;

                    case ElementType.Namespace:
                        Larvae.SetElement(t, GetElement() as string);
                        break;

                    default:
                        break;
                }
            }
        }
    }
}
