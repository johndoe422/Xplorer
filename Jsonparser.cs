using System;
using System.Collections.Generic;

public class JsonParser
{
    private string json;
    private int index;

    public JsonParser(string json)
    {
        this.json = json;
        this.index = 0;
    }

    public object Parse()
    {
        SkipWhitespace();
        if (json[index] == '{') return ParseObject();
        if (json[index] == '[') return ParseArray();
        throw new Exception("Invalid JSON");
    }

    private Dictionary<string, object> ParseObject()
    {
        var dict = new Dictionary<string, object>();
        index++;
        SkipWhitespace();
        while (json[index] != '}')
        {
            SkipWhitespace();
            var key = ParseString();
            SkipWhitespace();
            index++;
            SkipWhitespace();
            var value = ParseValue();
            dict[key] = value;
            SkipWhitespace();
            if (json[index] == ',') index++;
            SkipWhitespace();
        }
        index++;
        return dict;
    }

    private List<object> ParseArray()
    {
        var list = new List<object>();
        index++;
        SkipWhitespace();
        while (json[index] != ']')
        {
            var value = ParseValue();
            list.Add(value);
            SkipWhitespace();
            if (json[index] == ',') index++;
            SkipWhitespace();
        }
        index++;
        return list;
    }

    private object ParseValue()
    {
        SkipWhitespace();
        if (json[index] == '"') return ParseString();
        if (json[index] == '{') return ParseObject();
        if (json[index] == '[') return ParseArray();
        if (char.IsDigit(json[index]) || json[index] == '-') return ParseNumber();
        if (json.Substring(index, 4) == "true") { index += 4; return true; }
        if (json.Substring(index, 5) == "false") { index += 5; return false; }
        if (json.Substring(index, 4) == "null") { index += 4; return null; }
        throw new Exception("Invalid JSON value");
    }

    private string ParseString()
    {
        index++;
        int start = index;
        while (json[index] != '"') index++;
        string result = json.Substring(start, index - start);
        index++;
        return result;
    }

    private object ParseNumber()
    {
        int start = index;
        while (char.IsDigit(json[index]) || json[index] == '.' || json[index] == '-') index++;
        string numStr = json.Substring(start, index - start);
        if (numStr.Contains(".")) return double.Parse(numStr);
        return int.Parse(numStr);
    }

    private void SkipWhitespace()
    {
        while (char.IsWhiteSpace(json[index])) index++;
    }
}


