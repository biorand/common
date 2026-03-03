using System;
using System.Text;

namespace IntelOrca.Biohazard.BioRand
{
    public sealed class RandomizerLogger
    {
        private readonly StringBuilder _sb = new();
        private readonly string _hr = new('-', 80);
        private int _indent;

        public string Output => _sb.ToString();

        public void Push()
        {
            _indent++;
        }

        public void Push(string header)
        {
            LogLine(header);
            Push();
        }

        public void Pop()
        {
            _indent--;
        }

        public void LogVersionTimeInfo(string versionLine, string authorLine)
        {
            _sb.AppendLine(versionLine);
            _sb.AppendLine(authorLine);
            _sb.AppendLine($"Generated at {DateTime.Now}");
        }

        public void LogHr()
        {
            _sb.AppendLine(_hr);
        }

        public void LogHeader(string header)
        {
            _sb.AppendLine();
            LogHr();
            _sb.AppendLine(header);
            LogHr();
        }

        public void LogLine(string line)
        {
            _sb.Append(' ', _indent * 2);
            _sb.AppendLine(line);
        }

        public void LogLine(params object[] columns)
        {
            _sb.Append(' ', _indent * 2);
            if (columns.Length > 0)
            {
                foreach (var column in columns)
                {
                    _sb.Append(column);
                    _sb.Append(" ");
                }
                _sb.Remove(_sb.Length - 1, 1);
            }
            _sb.AppendLine();
        }
    }
}
