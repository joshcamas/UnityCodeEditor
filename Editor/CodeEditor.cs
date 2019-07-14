using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace Ardenfall.UnityCodeEditor
{
    [System.Serializable]
    public class CodeEditor
    {
        public string controlName { get; set; }
        public System.Action onValueChange;
        public int tabSpaces = 2;
        public System.Func<string, string> highlighter { get; set; }

        private TextEditor textEditor;
        private int textEditorId;
        private string cachedCode { get; set; }
        private string cachedHighlightedCode { get; set; }

        private CodeTheme theme;
        private GUIStyle numberLines;
        private FieldInfo cachedSelectAllField;

        private int charWidth = 11;
        private bool pressedTab = false;
        private bool pressedShift = false;

        public bool isFocused 
        {
            get { return GUI.GetNameOfFocusedControl() == controlName; }
        }

        public CodeEditor(string controlName,CodeTheme theme)
        {
            this.controlName = controlName;
            this.theme = theme;
            highlighter = code => code;
        }

        public string Update(string code)
        {
            if (textEditor == null || textEditor.controlID != textEditorId)
                return code;
            
            string editedCode = UpdateEditor(textEditor, code, pressedTab, pressedShift);

            pressedTab = false;
            pressedShift = false;

            return editedCode;
        }

        public string Draw(string code, GUIStyle style, params GUILayoutOption[] options)
        {
            var preBackgroundColor = GUI.backgroundColor;
            var preColor = GUI.color;
            Color preSelection = GUI.skin.settings.selectionColor;
            Color preCursor = GUI.skin.settings.cursorColor;
            float preFlashSpeed = GUI.skin.settings.cursorFlashSpeed;

            GUI.backgroundColor = GetColor(theme.background);
            GUI.color = GetColor(theme.color);
            GUI.skin.settings.selectionColor = GetColor(theme.selection);
            GUI.skin.settings.cursorColor = GetColor(theme.cursor);
            GUI.skin.settings.cursorFlashSpeed = 0;

            var backStyle = new GUIStyle(style);
            backStyle.normal.textColor = Color.clear;
            backStyle.hover.textColor = Color.clear;
            backStyle.active.textColor = Color.clear;
            backStyle.focused.textColor = Color.clear;
            
            backStyle.normal.background = EditorGUIUtility.whiteTexture;
            backStyle.hover.background = EditorGUIUtility.whiteTexture;
            backStyle.active.background = EditorGUIUtility.whiteTexture;
            backStyle.focused.background = EditorGUIUtility.whiteTexture;

            EditorGUILayout.BeginHorizontal();
            
            //Line Count
            DrawLineNumbers(code, style);

            //Succ the tab key
            bool usedTab = Event.current.type != EventType.Layout
                && (Event.current.keyCode == KeyCode.Tab || Event.current.character == '\t');

            //Update tab keys - we'll use these in the update loop
            pressedTab = usedTab && Event.current.type == EventType.KeyDown;
            pressedShift = Event.current.shift;

            if (usedTab)
                Event.current.Use();

            //Disable selecting all on mouse up 
            if (cachedSelectAllField == null)
                cachedSelectAllField = typeof(EditorGUI).GetField("s_SelectAllOnMouseUp", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Default);

            cachedSelectAllField.SetValue(null, false);

            GUI.SetNextControlName(controlName);
            string editedCode = EditorGUILayout.TextArea(code, backStyle, GUILayout.ExpandHeight(true));

            //Save reference to editor
            TextEditor foundTextEditor = typeof(EditorGUI)
              .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
              .GetValue(null) as TextEditor;

            //Make sure the found text editor matches our text area before using
            if (foundTextEditor != null)
            {
                if (foundTextEditor != null)
                    textEditorId = foundTextEditor.controlID;
                else
                    textEditorId = -1;

                textEditor = foundTextEditor;
            }

            if (editedCode != code) {
                code = editedCode;
                onValueChange?.Invoke();
            }

            if (cachedCode != code) {
                cachedCode = code;
                cachedHighlightedCode = highlighter(code);
            }

            //Render foreground (syntax highlighting)

            GUI.backgroundColor = Color.clear;

            var foreStyle = new GUIStyle(style);
            foreStyle.richText = true;
            
            foreStyle.normal.textColor = GUI.color;
            foreStyle.hover.textColor = GUI.color;
            foreStyle.active.textColor = GUI.color;
            foreStyle.focused.textColor = GUI.color;
        
            //Render
            EditorGUI.TextArea(GUILayoutUtility.GetLastRect(), cachedHighlightedCode, foreStyle);

            GUI.backgroundColor = preBackgroundColor;
            GUI.color = preColor;
            GUI.skin.settings.selectionColor = preSelection;
            GUI.skin.settings.cursorColor = preCursor;
            GUI.skin.settings.cursorFlashSpeed = preFlashSpeed;

            EditorGUILayout.EndHorizontal();

            return code;
        }

        private string UpdateEditorTabs(TextEditor editor,string content,bool shift)
        {
            //Detect strange issue with lengths, just return content
            if (content.Length <= editor.cursorIndex || content.Length <= editor.selectIndex)
                return content;

            //Build tab string
            string tabrep = "";

            for (int i = 0; i < tabSpaces; i++)
                tabrep += " ";

            //Selection case
            if (editor.selectIndex != editor.cursorIndex)
            {
                //Currently unsupported
                return content;
                /*
                //Only select multiple lines
                if(editor.SelectedText.Contains("\n"))
                {
                    int start = Mathf.RoundToInt(Mathf.Min(editor.cursorIndex, editor.selectIndex));
                    int length = Mathf.RoundToInt(Mathf.Abs(editor.cursorIndex - editor.selectIndex));

                    string selection = editor.SelectedText;
                    //Remove per line
                    if (shift)
                    {
                        selection = selection.Replace("\n"+ tabrep, "\n");
                    }
                    //Add per line
                    else
                    {
                        selection = selection.Replace("\n", "\n" + tabrep);
                    }

                    content = content.Remove(start, length);
                    content = content.Insert(start, selection);
                }*/
            }
            //Normal case
            else
            {
                //Add "tab"
                if(!shift)
                {
                    content = content.Insert(editor.cursorIndex, tabrep);
                    editor.cursorIndex += tabSpaces;
                    editor.selectIndex = editor.cursorIndex;

                //Remove "tab"
                } else if (editor.cursorIndex > tabSpaces)
                {
                    bool canRemove = true;

                    //Make sure there is a "tab" before the cursor
                    for (int i = 0; i < tabSpaces; i++)
                    {
                        if (content[editor.cursorIndex - i - 1] != ' ')
                            canRemove = false;
                    }

                    if(canRemove)
                    {
                        content = content.Remove(editor.cursorIndex - tabSpaces, tabSpaces);
                        editor.cursorIndex -= tabSpaces;
                        editor.selectIndex = editor.cursorIndex;
                    }
                }
            }

            return content;
        }

        string UpdateEditor(TextEditor editor,string content, bool pressedTab,bool shift)
        {
            if (editor == null)
                return content;

            if(pressedTab)
                content = UpdateEditorTabs(editor, content, shift);

            editor.text = content;

            return content;
        }

        private void DrawLineNumbers(string code, GUIStyle baseStyle)
        {
            int lineCount = code.Split('\n').Length;
            float lineCountWidth = (lineCount.ToString().Length) * charWidth;

            //Reserve space
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(lineCountWidth), GUILayout.ExpandHeight(true));

            string lineString = "";

            for (int i = 0; i < lineCount; i++)
                lineString += i + "\n";

            GUIStyle style = new GUIStyle(baseStyle);
            style.normal.textColor = Color.white;

            style.normal.background = EditorGUIUtility.whiteTexture;
            style.hover.background = EditorGUIUtility.whiteTexture;
            style.active.background = EditorGUIUtility.whiteTexture;
            style.focused.background = EditorGUIUtility.whiteTexture;

            style.alignment = TextAnchor.UpperRight;

            GUI.Label(rect, new GUIContent(lineString), style);
        }
     
        private Color GetColor(string colorCode)
        {
            Color color = Color.magenta;
            ColorUtility.TryParseHtmlString(colorCode, out color);
            return color;
        }
    }

}