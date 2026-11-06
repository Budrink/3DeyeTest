using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileProcessor.LineFormat
{
    public class EntryComparer : IComparer<Entry>
    {
        public int Compare(Entry x, Entry y)
        {
            var result = StringComparer.Ordinal.Compare(x.Text, y.Text);

            if (result != 0) return result;

            return x.Number.CompareTo(y.Number);
        }
    }
}
