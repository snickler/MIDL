﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MIDLParser
{
    public partial class Document
    {
        private static readonly Regex _rxComment = new(@"//.+");
        private static readonly Regex _rxString = new(@"\""[^\""].+\""");
        private static readonly Regex _rxType = new(@"\b(asm|__asm__|auto|bool|_Bool|char|_Complex|double|float|PWSTR|PCWSTR|_Imaginary|int|long|short|VARIANT|BSTR|string|String|Single|Double|Int16|Int32|Int64|UInt16|UInt32|UInt64|Char|Guid|Object)\b");
        private static readonly Regex _rxKeyword = new(@"\b(signed|typedef|union|unsigned|void|VARIANT|BSTR|break|case|continue|default|do|else|for|goto|if|_Pragma|return|switch|while|set|get|event|runtimeclass|namespace|interface|delegate|static|unsealed)\b");


        public bool IsParsing { get; private set; }
        public bool IsValid { get; private set; }

        public void Parse()
        {
            IsParsing = true;
            var isSuccess = false;
            var start = 0;

            try
            {
                List<ParseItem> tokens = new();

                foreach (var line in _lines)
                {
                    IEnumerable<ParseItem>? current = ParseLine(start, line);

                    if (current != null)
                    {
                        tokens.AddRange(current);
                    }

                    start += line.Length;
                }

                Items = tokens;

                ValidateDocument();

                isSuccess = true;
            }
            finally
            {
                IsParsing = false;

                if (isSuccess)
                {
                    Parsed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private IEnumerable<ParseItem> ParseLine(int start, string line)
        {
            var trimmedLine = line.TrimEnd();
            List<ParseItem> lineItems = new();

            // Comment
            if (IsMatch(_rxComment, trimmedLine, out Match matchComment))
            {
                lineItems.Add(ToParseItem(matchComment, start, ItemType.Comment)!);
            }

            // Strings
            if (IsMatches(_rxString, trimmedLine, out MatchCollection? matchStrings))
            {
                IEnumerable<ParseItem>? items = ToParseItems(matchStrings, start, ItemType.String)!;
                AddItem(lineItems, items);
            }

            // Keywords
            if (IsMatches(_rxKeyword, trimmedLine, out MatchCollection? matchVar))
            {
                IEnumerable<ParseItem>? items = ToParseItems(matchVar, start, ItemType.Keyword)!;
                AddItem(lineItems, items);
            }

            // Types
            if (IsMatches(_rxType, trimmedLine, out MatchCollection? matchType))
            {
                IEnumerable<ParseItem>? items = ToParseItems(matchType, start, ItemType.Type)!;
                AddItem(lineItems, items);
            }

            return lineItems;
        }

        private static void AddItem(List<ParseItem> items, IEnumerable<ParseItem> itemsToAdd)
        {
            foreach (ParseItem? item in itemsToAdd)
            {
                if (!items.Any(i => i.Contains(item.Start)))
                {
                    items.Add(item);
                }
            }
        }

        public static bool IsMatch(Regex regex, string line, out Match match)
        {
            match = regex.Match(line);
            return match.Success;
        }

        public static bool IsMatches(Regex regex, string line, out MatchCollection matches)
        {
            matches = regex.Matches(line);
            return matches.Count > 0;
        }

        private ParseItem ToParseItem(string line, int start, ItemType type)
        {
            var item = new ParseItem(start, line, this, type);


            return item;
        }

        private ParseItem? ToParseItem(Match match, int start, ItemType type)
        {
            if (string.IsNullOrEmpty(match.Value))
            {
                return null;
            }

            return ToParseItem(match.Value, start + match.Index, type);
        }

        private IEnumerable<ParseItem> ToParseItems(MatchCollection matches, int start, ItemType type)
        {
            foreach (Match match in matches)
            {
                ParseItem? item = ToParseItem(match, start, type);

                if (item != null)
                {
                    yield return item;
                }
            }
        }

        private void ValidateDocument()
        {
            IsValid = true;
            foreach (ParseItem item in Items.Where(i => i.Type == ItemType.Type))
            {
                if (_convertTypes.ContainsKey(item.Text))
                {
                    item.Errors.Add(Errors.PL001.WithFormat(item.Text, _convertTypes[item.Text]));
                    IsValid = false;
                }
            }
        }

        private Dictionary<string, string> _convertTypes = new()
        {
            {"int", "Int32"},
            {"short", "Int16"},
            {"long", "Int32"},
            {"PWSTR", "String"},
            {"PCWSTR", "String"},
            {"double", "Double"},
            {"float", "Single"},
            {"string", "String" },
        };

        private class Errors
        {
            public static Error PL001 { get; } = new("IDL001", "Use {1} instead of {0}.", ErrorCategory.Error);
            public static Error PL002 { get; } = new("PL002", "\"{0}\" is not a valid absolute URI", ErrorCategory.Warning);
        }


        public event EventHandler? Parsed;
    }
}
