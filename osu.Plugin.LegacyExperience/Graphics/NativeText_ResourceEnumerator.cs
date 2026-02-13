using System.Collections;
using System.Reflection;
using System.Resources;

namespace osu.Plugin.LegacyExperience.Graphics;

partial class NativeText
{
    // see comments in populateFontCollection
    private class ResourceEnumerator : IEnumerator<string>
    {
        private int index = -1;

        public string Current => currentString;

        object IEnumerator.Current => Current;

        private readonly ResourceReader reader;
        private readonly int numResources;

        private readonly MethodInfo AllocateStringForNameIndex_MethodInfo;

        public ResourceEnumerator(ResourceReader reader)
        {
            this.reader = reader;

            var readerType = reader.GetType();

            var numResources_FieldInfo = readerType
                .GetField("_numResources", BindingFlags.NonPublic | BindingFlags.Instance)!;

            AllocateStringForNameIndex_MethodInfo = readerType
                .GetMethod("AllocateStringForNameIndex", BindingFlags.NonPublic | BindingFlags.Instance)!;

            numResources = (int)numResources_FieldInfo.GetValue(reader)!;
        }

        private string currentString = null!;

        public bool MoveNext()
        {
            index++;
            if (index >= numResources)
                return false;

            currentString = (string)AllocateStringForNameIndex_MethodInfo.Invoke(reader, new object[] { index, null! })!;

            return true;
        }

        public void Reset()
        {
            index = -1;
            currentString = null!;
        }

        public void Dispose()
        {
        }
    }
}
