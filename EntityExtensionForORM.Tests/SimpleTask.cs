
using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace EntityExtensionForORM.Tests
{
    public class SimpleTask : Task
    {
        public override bool Execute()
        {
            return true;

        private string myProperty;
        public string MyProperty
        {
            get { return myProperty; }
            set { myProperty = value; }
        }
        }
    }
}
