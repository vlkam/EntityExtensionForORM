﻿
using SQLite.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EntityExtensionForORM
{
    // https://lostechies.com/jimmybogard/2008/08/12/enumeration-classes/
    public abstract class Enumeration : IComparable,ISerializable<int>
    {
        private readonly int _value;
        private readonly string _displayName;

        public Enumeration(int value)
        {
            _value = value;
        }

        public Enumeration() { }

        protected Enumeration(int value, string displayName)
        {
            _value = value;
            _displayName = displayName;
        }

        public int Value
        {
            get { return _value; }
        }

        public string DisplayName
        {
            get { return _displayName; }
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public static IEnumerable<T> GetAll<T>() where T : Enumeration, new()
        {
            var type = typeof (T);
            var fields = type.GetRuntimeFields(); //(BindingFlags.Public | BindingFlags.Static
                //| BindingFlags.DeclaredOnly
                //);

            foreach (var info in fields)
            {
                if( !(info.IsPublic | info.IsStatic)) continue;
                
                var instance = new T();
                var locatedValue = info.GetValue(instance) as T;

                if (locatedValue != null)
                {
                    yield return locatedValue;
                }
            }
        }

        public override bool Equals(object obj)
        {
            var otherValue = obj as Enumeration;

            if (otherValue == null)
            {
                return false;
            }

            var typeMatches = GetType().Equals(obj.GetType());
            var valueMatches = _value.Equals(otherValue.Value);

            return typeMatches && valueMatches;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public static int AbsoluteDifference(Enumeration firstValue, Enumeration secondValue)
        {
            var absoluteDifference = Math.Abs(firstValue.Value - secondValue.Value);
            return absoluteDifference;
        }

        public static T FromValue<T>(int value) where T : Enumeration, new()
        {
            var matchingItem = parse<T, int>(value, "value", item => item.Value == value);
            return matchingItem;
        }

        public static T FromDisplayName<T>(string displayName) where T : Enumeration, new()
        {
            var matchingItem = parse<T, string>(displayName, "display name", item => item.DisplayName == displayName);
            return matchingItem;
        }

        private static T parse<T, K>(K value, string description, Func<T, bool> predicate) where T : Enumeration, new()
        {
            var matchingItem = GetAll<T>().FirstOrDefault(predicate);

            if (matchingItem == null)
            {
                var message = string.Format("'{0}' is not a valid {1} in {2}", value, description, typeof (T));
                throw new Exception(message);
            }

            return matchingItem;
        }

        public int CompareTo(object other) => Value.CompareTo(((Enumeration) other).Value);
        
        public int Serialize() => _value;

    }
}



