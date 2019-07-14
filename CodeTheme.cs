using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace Ardenfall.UnityCodeEditor
{
    public class CodeTheme
    {
        public virtual string background { get; }
        public virtual string color { get; }
        public virtual string selection { get; }
        public virtual string cursor { get; }
    }

}