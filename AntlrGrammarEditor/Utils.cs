using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public static class Utils
    {
        public static int LineColumnToLinear(string text, int line, int column)
        {
            int currentLine = 1;
            int currentColumn = 0;

            int i = 0;
            try
            {
                while (currentLine != line || currentLine == line && currentColumn != column)
                {
                    // General line endings:
                    //  Windows: '\r\n'
                    //  Mac (OS 9-): '\r'
                    //  Mac (OS 10+): '\n'
                    //  Unix/Linux: '\n'

                    switch (text[i])
                    {
                        case '\r':
                            currentLine++;
                            currentColumn = 0;
                            if (i + 1 < text.Length && text[i + 1] == '\n')
                            {
                                i++;
                            }
                            break;

                        case '\n':
                            currentLine++;
                            currentColumn = 0;
                            break;

                        default:
                            currentColumn++;
                            break;
                    }

                    i++;
                }
            }
            catch
            {
            }

            return i;
        }

        public static void LinearToLineColumn(int index, string text, out int line, out int column)
        {
            line = 1;
            column = 0;

            try
            {
                int i = 0;
                while (i != index)
                {
                    // General line endings:
                    //  Windows: '\r\n'
                    //  Mac (OS 9-): '\r'
                    //  Mac (OS 10+): '\n'
                    //  Unix/Linux: '\n'

                    switch (text[i])
                    {
                        case '\r':
                            line++;
                            column = 0;
                            if (i + 1 < text.Length && text[i + 1] == '\n')
                            {
                                i++;
                            }
                            break;

                        case '\n':
                            line++;
                            column = 0;
                            break;

                        default:
                            column++;
                            break;
                    }
                    i++;
                }
            }
            catch
            {
            }
        }
    }
}
