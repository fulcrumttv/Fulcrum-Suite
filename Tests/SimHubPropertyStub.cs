// Only compiled into the regression executable, NEVER the distributed plugin.
using System;
using System.Collections.Generic;

namespace SimHub.Plugins
{
    public sealed class PluginManager
    {
        private readonly Dictionary<string, object> properties = new Dictionary<string, object>();
        public void AddProperty(string name, Type type, object value, string description)
        {
            properties[name] = value;
        }
        public void SetPropertyValue(string name, Type type, object value)
        {
            if (!properties.ContainsKey(name)) throw new Exception("Unregistered property: " + name);
            properties[name] = value;
        }
        public object Get(string name)
        {
            return properties[name];
        }
    }
}
