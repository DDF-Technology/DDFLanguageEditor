using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            richTextBoxLineNumbers.ReadOnly = true;
            richTextBoxLineNumbers.BackColor = richTextBoxMainEditor.BackColor;
            richTextBoxLineNumbers.Font = richTextBoxMainEditor.Font;
            richTextBoxLineNumbers.SelectionAlignment = HorizontalAlignment.Center;
            richTextBoxLineNumbers.ScrollBars = RichTextBoxScrollBars.None;

            richTextBox_TextChanged(sender, e);
        }

        private void richTextBox_TextChanged(object sender, EventArgs e)
        {
            int selectionStart = richTextBoxMainEditor.SelectionStart;
            int selectionLength = richTextBoxMainEditor.SelectionLength;

            richTextBoxMainEditor.SuspendLayout();

            // Reset the formatting
            richTextBoxMainEditor.SelectAll();
            richTextBoxMainEditor.SelectionColor = Color.White;
            richTextBoxMainEditor.SelectionFont = new Font(richTextBoxMainEditor.Font, FontStyle.Bold);

            // Apply formatting for keywords
            keywordTextFormatting(DictRules.commentSingleRow, DictRules.commentColor);
            keywordTextFormatting(DictRules.grammar, DictRules.grammarColor);
            keywordTextFormatting(DictRules.number, DictRules.numberColor);
            keywordTextFormatting(DictRules.dataType, DictRules.dataTypeColor);
            keywordTextFormatting(DictRules.dataTypeComplex, DictRules.dataTypeComplexColor);
            keywordTextFormatting(DictRules.baseOperator, DictRules.baseOperatorColor);
            keywordTextFormatting(DictRules.mathOperator, DictRules.mathOperatorColor);
            keywordTextFormatting(DictRules.logicOperator, DictRules.logicOperatorColor);
            keywordTextFormatting(DictRules.booleanOperator, DictRules.booleanOperatorColor);
            keywordTextFormatting(DictRules.functionOperator, DictRules.functionOperatorColor);
            keywordTextFormatting(DictRules.flushOperator, DictRules.flushOperatorColor);

            // Apply formatting for block
            blockTextFormatting(DictRules.libraryStart, DictRules.libraryEnd, DictRules.libraryColor);
            blockTextFormatting(DictRules.commentStart, DictRules.commentEnd, DictRules.commentColor);            
            blockTextFormatting(DictRules.stringStart, DictRules.stringEnd, DictRules.stringColor);

            richTextBoxMainEditor.Select(selectionStart, selectionLength);
            richTextBoxMainEditor.ResumeLayout();

            richTextBoxLineNumbers.Text = "";
            updateLineNumbers();
        }

        private void richTextBoxMainEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab && !e.Shift)
            {
                e.SuppressKeyPress = true; // Evita il comportamento predefinito del TAB

                int tabSize = 4; // Numero di spazi per il TAB
                string spaces = new string(' ', tabSize);

                // Inserisce gli spazi al posto del TAB
                int selectionStart = richTextBoxMainEditor.SelectionStart;
                richTextBoxMainEditor.Text = richTextBoxMainEditor.Text.Insert(selectionStart, spaces);
                richTextBoxMainEditor.SelectionStart = selectionStart + tabSize;
            }
            else if (e.KeyCode == Keys.Tab && e.Shift)
            {
                e.SuppressKeyPress = true; // Evita il comportamento predefinito del Back TAB

                int tabSize = 4; // Numero di spazi per il Back TAB

                // Gestisce rimuovendo spazi indietro
                int selectionStart = richTextBoxMainEditor.SelectionStart;

                if (selectionStart >= tabSize)
                {
                    // Controlla se ci sono esattamente `tabSize` spazi prima della posizione corrente
                    string textBeforeCursor = richTextBoxMainEditor.Text.Substring(selectionStart - tabSize, tabSize);

                    if (textBeforeCursor == new string(' ', tabSize))
                    {
                        // Rimuove i `tabSize` spazi
                        richTextBoxMainEditor.Text = richTextBoxMainEditor.Text.Remove(selectionStart - tabSize, tabSize);
                        richTextBoxMainEditor.SelectionStart = selectionStart - tabSize;
                    }
                }
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Evita il comportamento predefinito di "Invio"

                // Ottieni la posizione del cursore prima dell'Enter
                int selectionStart = richTextBoxMainEditor.SelectionStart;

                // Ottieni il contenuto della riga corrente
                int currentLineIndex = richTextBoxMainEditor.GetLineFromCharIndex(selectionStart);
                string currentLine = getLineText(currentLineIndex);

                // Calcola la spaziatura iniziale (tab/spazi) della riga corrente
                string leadingWhitespace = getLeadingWhitespace(currentLine);

                // Verifica se la riga termina con { o (
                bool addAdditionalTab = currentLine.TrimEnd().EndsWith("{") || currentLine.TrimEnd().EndsWith("(");

                // Aggiungi la nuova riga con la stessa indentazione
                richTextBoxMainEditor.Text = richTextBoxMainEditor.Text.Insert(selectionStart, "\n" + leadingWhitespace);

                // Se necessario, aggiungi un ulteriore TAB
                if (addAdditionalTab)
                {
                    int tabSize = 4;
                    string additionalTab = new string(' ', tabSize);
                    richTextBoxMainEditor.Text = richTextBoxMainEditor.Text.Insert(selectionStart + leadingWhitespace.Length + 1, additionalTab);
                    selectionStart += additionalTab.Length; // Sposta il cursore dopo il nuovo TAB
                }

                // Ripristina la posizione del cursore alla fine della nuova riga
                richTextBoxMainEditor.SelectionStart = selectionStart + 1 + leadingWhitespace.Length;
            }
        }

        private void richTextBoxMainEditor_VScroll(object sender, EventArgs e)
        {
            richTextBoxLineNumbers.Text = "";
            updateLineNumbers();
        }

        private void richTextBoxMainEditor_FontChanged(object sender, EventArgs e)
        {
            updateLineNumbers();
        }

        //----------------------------------------------------------------------------------------

        private void keywordTextFormatting(string[] keywords, Color keywordColor)
        {
            foreach (var keyword in keywords)
            {
                // Prepara la regex per le parole chiave con lettere e numeri
                string pattern;
                if (Regex.IsMatch(keyword, @"^[A-Za-z_]+$")) //@"^\w+$")
                {
                    pattern = @"\b" + Regex.Escape(keyword) + @"\b";
                }
                else
                {
                    pattern = Regex.Escape(keyword);
                }

                Regex regex = new Regex(pattern);

                foreach (Match match in regex.Matches(richTextBoxMainEditor.Text))
                {
                    // Seleziona il testo corrispondente e applica il colore e il font
                    richTextBoxMainEditor.Select(match.Index, match.Length);
                    richTextBoxMainEditor.SelectionColor = keywordColor;
                    richTextBoxMainEditor.SelectionFont = new Font(richTextBoxMainEditor.Font, FontStyle.Bold);
                }
            }
        }

        private void blockTextFormatting(string blockStart, string blockEnd, Color blockColor)
        {
            int start = richTextBoxMainEditor.Text.IndexOf(blockStart);
            while (start != -1)
            {
                int end = richTextBoxMainEditor.Text.IndexOf(blockEnd, start + blockStart.Length);
                if (end == -1)
                {
                    break;
                }

                int length = end - start + blockEnd.Length;
                richTextBoxMainEditor.Select(start, length);
                richTextBoxMainEditor.SelectionColor = blockColor;
                richTextBoxMainEditor.SelectionFont = new Font(richTextBoxMainEditor.Font, FontStyle.Bold);

                start = richTextBoxMainEditor.Text.IndexOf(blockStart, end + blockEnd.Length);
            }
        }        

        private string getLineText(int lineIndex)
        {
            string[] lines = richTextBoxMainEditor.Lines;
            if (lineIndex >= 0 && lineIndex < lines.Length)
            {
                return lines[lineIndex];
            }
            return string.Empty;
        }

        private string getLeadingWhitespace(string line)
        {
            int index = 0;
            while (index < line.Length && char.IsWhiteSpace(line[index]))
            {
                index++;
            }
            return line.Substring(0, index);
        }

        private void updateLineNumbers()
        {
            Point pt = new Point(0, 0);
            int firstIndex = richTextBoxMainEditor.GetCharIndexFromPosition(pt);
            int firstLine = richTextBoxMainEditor.GetLineFromCharIndex(firstIndex);
            Point pt2 = new Point(ClientRectangle.Width, ClientRectangle.Height);
            int lastIndex = richTextBoxMainEditor.GetCharIndexFromPosition(pt2);
            int lastLine = richTextBoxMainEditor.GetLineFromCharIndex(lastIndex);            
            int lineCount = lastLine - firstLine + 1;
            for (int i = firstLine; i <= lastLine + 1; i++)
            {
                richTextBoxLineNumbers.Text += (i + 1) + "\n";
            }
        }
    }
}



