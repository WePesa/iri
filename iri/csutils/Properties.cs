﻿namespace iri.utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    internal class Properties
    {
        private Dictionary<String, String> list;

        private String filename;

        public Properties(String file)
        {
            reload(file);
        }

        public String get(String field, String defValue)
        {
            return (get(field) == null) ? (defValue) : (get(field));
        }
        public String get(String field)
        {
            return (list.ContainsKey(field)) ? (list[field]) : (null);
        }

        public void set(String field, String value)
        {
            field = trimUnwantedChars(field);
            value = trimUnwantedChars(value);

            if (!list.ContainsKey(field))
                list.Add(field, value.ToString());
            else
                list[field] = value.ToString();
        }

        public string trimUnwantedChars(string toTrim)
        {
            toTrim = toTrim.Replace(";", string.Empty);
            toTrim = toTrim.Replace("#", string.Empty);
            toTrim = toTrim.Replace("'", string.Empty);
            toTrim = toTrim.Replace("=", string.Empty);
            return toTrim;
        }

        public void Save()
        {
            Save(filename);
        }

        public void Save(String filename)
        {
            this.filename = filename;

            if (!File.Exists(filename))
                File.Create(filename);

            StreamWriter file = new StreamWriter(filename);

            foreach (String prop in list.Keys.ToArray())
                if (!String.IsNullOrWhiteSpace(list[prop]))
                    file.WriteLine(prop + "=" + list[prop]);

            file.Close();
        }

        public void reload()
        {
            reload(filename);
        }

        public void reload(String filename)
        {
            this.filename = filename;
            list = new Dictionary<String, String>();

            if (File.Exists(filename))
                loadFromFile(filename);
            else
                File.Create(filename);
        }

        private void loadFromFile(String file)
        {
            foreach (String line in File.ReadAllLines(file))
            {
                if ((!String.IsNullOrEmpty(line)) &&
                    (!line.StartsWith(";")) &&
                    (!line.StartsWith("#")) &&
                    (!line.StartsWith("'")) &&
                    (line.Contains('=')))
                {
                    int index = line.IndexOf('=');
                    String key = line.Substring(0, index).Trim();
                    String value = line.Substring(index + 1).Trim();

                    if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                        (value.StartsWith("'") && value.EndsWith("'")))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    try
                    {
                        //ignore duplicates
                        list.Add(key, value);
                    }
                    catch { }
                }
            }
        }
    }
}