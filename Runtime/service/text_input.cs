﻿using System;
using System.Collections.Generic;
using Unity.UIWidgets.foundation;
using UnityEngine;

namespace Unity.UIWidgets.service {
    public class TextEditingValue : IEquatable<TextEditingValue> {
        public readonly string text;
        public readonly TextSelection selection;
        public readonly TextRange composing;

        public TextEditingValue(string text = "", TextSelection selection = null, TextRange composing = null) {
            this.text = text;
            this.selection = selection ?? TextSelection.collapsed(-1);
            this.composing = composing ?? TextRange.empty;
        }

        public TextEditingValue copyWith(string text = null, TextSelection selection = null,
            TextRange composing = null) {
            return new TextEditingValue(
                text ?? this.text, selection ?? this.selection, composing ?? this.composing
            );
        }

        public TextEditingValue insert(string text) {
            string newText;
            TextSelection newSelection;
            if (string.IsNullOrEmpty(text)) {
                return this;
            }
            newText = this.selection.textBefore(this.text) + text + this.selection.textAfter(this.text);
            newSelection = TextSelection.collapsed(this.selection.start + text.Length);
            return new TextEditingValue(
                text: newText, selection: newSelection, composing: TextRange.empty
            );
        }

        public TextEditingValue deleteSelection(bool backDelete = true) {
            if (this.selection.isCollapsed) {
                if (backDelete) {
                    if (this.selection.start == 0) {
                        return this;
                    }
                    return this.copyWith(
                        text: this.text.Substring(0, this.selection.start - 1) + this.selection.textAfter(this.text),
                        selection: TextSelection.collapsed(this.selection.start - 1));
                }

                if (this.selection.start >= this.text.Length) {
                    return this;
                }
                return this.copyWith(text: this.text.Substring(0, this.selection.start) +
                                           this.text.Substring(this.selection.start + 1));
            } else {
                var newText = this.selection.textBefore(this.text) + this.selection.textAfter(this.text);
                return this.copyWith(text: newText, selection: TextSelection.collapsed(this.selection.start));
            }
        }

        public TextEditingValue moveLeft() {
            return this.moveSelection(-1);
        }

        public TextEditingValue moveRight() {
            return this.moveSelection(1);
        }

        public TextEditingValue extendLeft() {
            return this.moveExtent(-1);
        }

        public TextEditingValue extendRight() {
            return this.moveExtent(1);
        }

        public TextEditingValue moveExtent(int move) {
            int offset = this.selection.extentOffset + move;
            offset = Math.Max(0, offset);
            offset = Math.Min(offset, this.text.Length);
            return this.copyWith(selection: this.selection.copyWith(extentOffset: offset));
        }

        public TextEditingValue moveSelection(int move) {
            int offset = this.selection.baseOffset + move;
            offset = Math.Max(0, offset);
            offset = Math.Min(offset, this.text.Length);
            return this.copyWith(selection: TextSelection.collapsed(offset, affinity: this.selection.affinity));
        }

        public TextEditingValue compose(string composeText) {
            D.assert(!string.IsNullOrEmpty(composeText));
            var composeStart = this.composing == TextRange.empty ? this.selection.start : this.composing.start;
            var lastComposeEnd = this.composing == TextRange.empty ? this.selection.end : this.composing.end;
            var newText = this.text.Substring(0, composeStart) + composeText + this.text.Substring(lastComposeEnd);
            var componseEnd = composeStart + composeText.Length;
            return new TextEditingValue(
                text: newText, selection: TextSelection.collapsed(componseEnd),
                composing: new TextRange(composeStart, componseEnd)
            );
        }

        public TextEditingValue clearCompose() {
            if (this.composing == TextRange.empty) {
                return this;
            }
            return new TextEditingValue(
                text: this.text.Substring(0, this.composing.start) + this.text.Substring(this.composing.end),
                selection: TextSelection.collapsed(this.composing.start),
                composing: TextRange.empty
            );
        }

        public static readonly TextEditingValue empty = new TextEditingValue();

        public bool Equals(TextEditingValue other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }
            if (ReferenceEquals(this, other)) {
                return true;
            }
            return string.Equals(this.text, other.text) && Equals(this.selection, other.selection) &&
                   Equals(this.composing, other.composing);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            if (obj.GetType() != this.GetType()) {
                return false;
            }
            return this.Equals((TextEditingValue) obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = (this.text != null ? this.text.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.selection != null ? this.selection.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.composing != null ? this.composing.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(TextEditingValue left, TextEditingValue right) {
            return Equals(left, right);
        }

        public static bool operator !=(TextEditingValue left, TextEditingValue right) {
            return !Equals(left, right);
        }

        public override string ToString() {
            return $"Text: {this.text}, Selection: {this.selection}, Composing: {this.composing}";
        }
    }

    public interface TextInputClient {
        void updateEditingValue(TextEditingValue value);

        TextEditingValue getValueForOperation(TextEditOp operation);
        // void performAction(TextInputAction action);
    }

    public enum TextInputAction {
        done,
        newline,
    }

    public class TextInputConnection {
        internal TextInputConnection(TextInputClient client, TextInput textInput) {
            D.assert(client != null);
            D.assert(textInput != null);
            this._client = client;
            this._textInput = textInput;
            this._id = _nextId++;
        }

        public bool attached {
            get { return this._textInput._currentConnection == this; }
        }

        public void setEditingState(TextEditingValue value) {
            D.assert(this.attached);
            this._textInput._value = value;
        }

        public void setCompositionCursorPos(double x, double y) {
            D.assert(this.attached);
            this._textInput.setCompositionCursorPos(x, y);
        }

        public void close() {
            if (this.attached) {
                this._textInput._currentConnection = null;
                this._textInput._value = null;
                Input.imeCompositionMode = IMECompositionMode.Auto;
            }
            D.assert(!this.attached);
        }

        static int _nextId = 1;
        internal readonly int _id;
        internal readonly TextInputClient _client;
        internal readonly TextInput _textInput;
    }

    public class TextInput {
        internal TextInputConnection _currentConnection;
        internal TextEditingValue _value;
        static Dictionary<Event, TextEditOp> s_Keyactions;
        string _lastCompositionString;

        public TextInputConnection attach(TextInputClient client) {
            D.assert(client != null);
            var connection = new TextInputConnection(client, this);
            this._currentConnection = connection;
            Input.imeCompositionMode = IMECompositionMode.On;
            return connection;
        }

        public void OnGUI() {
            if (this._currentConnection == null) {
                return;
            }

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown) {
                bool handled = this.handleKeyEvent(currentEvent);
                if (!handled) {
                    if (currentEvent.keyCode == KeyCode.None) {
                        this._value = this._value.clearCompose().insert(new string(currentEvent.character, 1));
                        this._currentConnection._client.updateEditingValue(this._value);
                    }
                }
                currentEvent.Use();
            }

            if (!string.IsNullOrEmpty(Input.compositionString) &&
                this._lastCompositionString != Input.compositionString) {
                this._value = this._value.compose(Input.compositionString);
                this._currentConnection._client.updateEditingValue(this._value);
            }

            this._lastCompositionString = Input.compositionString;
        }

        public void setCompositionCursorPos(double x, double y) {
            Input.compositionCursorPos = new Vector2((float) x, (float) y);
        }

        bool handleKeyEvent(Event e) {
            initKeyActions();
            EventModifiers m = e.modifiers;
            e.modifiers &= ~EventModifiers.CapsLock;
            if (s_Keyactions.ContainsKey(e)) {
                TextEditOp op = s_Keyactions[e];
                var newValue = this._currentConnection._client.getValueForOperation(op);
                if (this._value != newValue) {
                    this._value = newValue;
                    this._currentConnection._client.updateEditingValue(this._value);
                }
                e.modifiers = m;
                return true;
            }
            e.modifiers = m;
            return false;
        }

        TextEditingValue performOperation(TextEditOp operation) {
            switch (operation) {
                case TextEditOp.MoveLeft:
                    return this._value.moveLeft();
                case TextEditOp.MoveRight:
                    return this._value.moveRight();
//                case TextEditOp.MoveUp:             MoveUp(); break;
//                case TextEditOp.MoveDown:           MoveDown(); break;
//                case TextEditOp.MoveLineStart:      MoveLineStart(); break;
//                case TextEditOp.MoveLineEnd:        MoveLineEnd(); break;
//                case TextEditOp.MoveWordRight:      MoveWordRight(); break;
//                case TextEditOp.MoveToStartOfNextWord:      MoveToStartOfNextWord(); break;
//                case TextEditOp.MoveToEndOfPreviousWord:        MoveToEndOfPreviousWord(); break;
//                case TextEditOp.MoveWordLeft:       MoveWordLeft(); break;
//                case TextEditOp.MoveTextStart:      MoveTextStart(); break;
//                case TextEditOp.MoveTextEnd:        MoveTextEnd(); break;
//                case TextEditOp.MoveParagraphForward:   MoveParagraphForward(); break;
//                case TextEditOp.MoveParagraphBackward:  MoveParagraphBackward(); break;
//                case TextEditOp.MoveGraphicalLineStart: MoveGraphicalLineStart(); break;
//                case TextEditOp.MoveGraphicalLineEnd: MoveGraphicalLineEnd(); break;
                case TextEditOp.SelectLeft:
                    return this._value.extendLeft();
                case TextEditOp.SelectRight:
                    return this._value.extendRight();
//                case TextEditOp.SelectUp:           SelectUp(); break;
//                case TextEditOp.SelectDown:         SelectDown(); break;
//                case TextEditOp.SelectWordRight:        SelectWordRight(); break;
//                case TextEditOp.SelectWordLeft:     SelectWordLeft(); break;
//                case TextEditOp.SelectToEndOfPreviousWord:  SelectToEndOfPreviousWord(); break;
//                case TextEditOp.SelectToStartOfNextWord:    SelectToStartOfNextWord(); break;
//
//                case TextEditOp.SelectTextStart:        SelectTextStart(); break;
//                case TextEditOp.SelectTextEnd:      SelectTextEnd(); break;
//                case TextEditOp.ExpandSelectGraphicalLineStart: ExpandSelectGraphicalLineStart(); break;
//                case TextEditOp.ExpandSelectGraphicalLineEnd: ExpandSelectGraphicalLineEnd(); break;
//                case TextEditOp.SelectParagraphForward:     SelectParagraphForward(); break;
//                case TextEditOp.SelectParagraphBackward:    SelectParagraphBackward(); break;
//                case TextEditOp.SelectGraphicalLineStart: SelectGraphicalLineStart(); break;
//                case TextEditOp.SelectGraphicalLineEnd: SelectGraphicalLineEnd(); break;
//                case TextEditOp.Delete:                             return Delete();
                case TextEditOp.Backspace:
                    return this._value.deleteSelection();
                // _value.composing
                // _value = _value.
//                case TextEditOp.Cut:                                    return Cut();
//                case TextEditOp.Copy:                               Copy(); break;
//                case TextEditOp.Paste:                              return Paste();
//                case TextEditOp.SelectAll:                          SelectAll(); break;
//                case TextEditOp.SelectNone:                     SelectNone(); break;
//                case TextEditOp.DeleteWordBack: return DeleteWordBack(); // break; // The uncoditional return makes the "break;" issue a warning about unreachable code
//                case TextEditOp.DeleteLineBack: return DeleteLineBack();
//                case TextEditOp.DeleteWordForward: return DeleteWordForward(); // break; // The uncoditional return makes the "break;" issue a warning about unreachable code
                default:
                    Debug.Log("Unimplemented: " + operation);
                    break;
            }

            return this._value;
        }


        static void initKeyActions() {
            if (s_Keyactions != null) {
                return;
            }
            s_Keyactions = new Dictionary<Event, TextEditOp>();

            // key mappings shared by the platforms
            mapKey("left", TextEditOp.MoveLeft);
            mapKey("right", TextEditOp.MoveRight);
            mapKey("up", TextEditOp.MoveUp);
            mapKey("down", TextEditOp.MoveDown);

            mapKey("#left", TextEditOp.SelectLeft);
            mapKey("#right", TextEditOp.SelectRight);
            mapKey("#up", TextEditOp.SelectUp);
            mapKey("#down", TextEditOp.SelectDown);

            mapKey("delete", TextEditOp.Delete);
            mapKey("backspace", TextEditOp.Backspace);
            mapKey("#backspace", TextEditOp.Backspace);

            // OSX is the special case for input shortcuts
            if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX) {
                // Keyboard mappings for mac
                // TODO     mapKey ("home", TextEditOp.ScrollStart);
                // TODO     mapKey ("end", TextEditOp.ScrollEnd);
                // TODO     mapKey ("page up", TextEditOp.ScrollPageUp);
                // TODO     mapKey ("page down", TextEditOp.ScrollPageDown);

                mapKey("^left", TextEditOp.MoveGraphicalLineStart);
                mapKey("^right", TextEditOp.MoveGraphicalLineEnd);
                // TODO     mapKey ("^up", TextEditOp.ScrollPageUp);
                // TODO     mapKey ("^down", TextEditOp.ScrollPageDown);

                mapKey("&left", TextEditOp.MoveWordLeft);
                mapKey("&right", TextEditOp.MoveWordRight);
                mapKey("&up", TextEditOp.MoveParagraphBackward);
                mapKey("&down", TextEditOp.MoveParagraphForward);

                mapKey("%left", TextEditOp.MoveGraphicalLineStart);
                mapKey("%right", TextEditOp.MoveGraphicalLineEnd);
                mapKey("%up", TextEditOp.MoveTextStart);
                mapKey("%down", TextEditOp.MoveTextEnd);

                mapKey("#home", TextEditOp.SelectTextStart);
                mapKey("#end", TextEditOp.SelectTextEnd);
                // TODO         mapKey ("#page up", TextEditOp.SelectPageUp);
                // TODO         mapKey ("#page down", TextEditOp.SelectPageDown);

                mapKey("#^left", TextEditOp.ExpandSelectGraphicalLineStart);
                mapKey("#^right", TextEditOp.ExpandSelectGraphicalLineEnd);
                mapKey("#^up", TextEditOp.SelectParagraphBackward);
                mapKey("#^down", TextEditOp.SelectParagraphForward);

                mapKey("#&left", TextEditOp.SelectWordLeft);
                mapKey("#&right", TextEditOp.SelectWordRight);
                mapKey("#&up", TextEditOp.SelectParagraphBackward);
                mapKey("#&down", TextEditOp.SelectParagraphForward);

                mapKey("#%left", TextEditOp.ExpandSelectGraphicalLineStart);
                mapKey("#%right", TextEditOp.ExpandSelectGraphicalLineEnd);
                mapKey("#%up", TextEditOp.SelectTextStart);
                mapKey("#%down", TextEditOp.SelectTextEnd);

                mapKey("%a", TextEditOp.SelectAll);
                mapKey("%x", TextEditOp.Cut);
                mapKey("%c", TextEditOp.Copy);
                mapKey("%v", TextEditOp.Paste);

                // emacs-like keybindings
                mapKey("^d", TextEditOp.Delete);
                mapKey("^h", TextEditOp.Backspace);
                mapKey("^b", TextEditOp.MoveLeft);
                mapKey("^f", TextEditOp.MoveRight);
                mapKey("^a", TextEditOp.MoveLineStart);
                mapKey("^e", TextEditOp.MoveLineEnd);

                mapKey("&delete", TextEditOp.DeleteWordForward);
                mapKey("&backspace", TextEditOp.DeleteWordBack);
                mapKey("%backspace", TextEditOp.DeleteLineBack);
            } else {
                // Windows/Linux keymappings
                mapKey("home", TextEditOp.MoveGraphicalLineStart);
                mapKey("end", TextEditOp.MoveGraphicalLineEnd);
                // TODO     mapKey ("page up", TextEditOp.MovePageUp);
                // TODO     mapKey ("page down", TextEditOp.MovePageDown);

                mapKey("%left", TextEditOp.MoveWordLeft);
                mapKey("%right", TextEditOp.MoveWordRight);
                mapKey("%up", TextEditOp.MoveParagraphBackward);
                mapKey("%down", TextEditOp.MoveParagraphForward);

                mapKey("^left", TextEditOp.MoveToEndOfPreviousWord);
                mapKey("^right", TextEditOp.MoveToStartOfNextWord);
                mapKey("^up", TextEditOp.MoveParagraphBackward);
                mapKey("^down", TextEditOp.MoveParagraphForward);

                mapKey("#^left", TextEditOp.SelectToEndOfPreviousWord);
                mapKey("#^right", TextEditOp.SelectToStartOfNextWord);
                mapKey("#^up", TextEditOp.SelectParagraphBackward);
                mapKey("#^down", TextEditOp.SelectParagraphForward);

                mapKey("#home", TextEditOp.SelectGraphicalLineStart);
                mapKey("#end", TextEditOp.SelectGraphicalLineEnd);
                // TODO         mapKey ("#page up", TextEditOp.SelectPageUp);
                // TODO         mapKey ("#page down", TextEditOp.SelectPageDown);

                mapKey("^delete", TextEditOp.DeleteWordForward);
                mapKey("^backspace", TextEditOp.DeleteWordBack);
                mapKey("%backspace", TextEditOp.DeleteLineBack);

                mapKey("^a", TextEditOp.SelectAll);
                mapKey("^x", TextEditOp.Cut);
                mapKey("^c", TextEditOp.Copy);
                mapKey("^v", TextEditOp.Paste);
                mapKey("#delete", TextEditOp.Cut);
                mapKey("^insert", TextEditOp.Copy);
                mapKey("#insert", TextEditOp.Paste);
            }
        }

        static void mapKey(string key, TextEditOp action) {
            s_Keyactions[Event.KeyboardEvent(key)] = action;
        }
    }

    public enum TextEditOp {
        MoveLeft,
        MoveRight,
        MoveUp,
        MoveDown,
        MoveLineStart,
        MoveLineEnd,
        MoveTextStart,
        MoveTextEnd,
        MovePageUp,
        MovePageDown,
        MoveGraphicalLineStart,
        MoveGraphicalLineEnd,
        MoveWordLeft,
        MoveWordRight,
        MoveParagraphForward,
        MoveParagraphBackward,
        MoveToStartOfNextWord,
        MoveToEndOfPreviousWord,
        SelectLeft,
        SelectRight,
        SelectUp,
        SelectDown,
        SelectTextStart,
        SelectTextEnd,
        SelectPageUp,
        SelectPageDown,
        ExpandSelectGraphicalLineStart,
        ExpandSelectGraphicalLineEnd,
        SelectGraphicalLineStart,
        SelectGraphicalLineEnd,
        SelectWordLeft,
        SelectWordRight,
        SelectToEndOfPreviousWord,
        SelectToStartOfNextWord,
        SelectParagraphBackward,
        SelectParagraphForward,
        Delete,
        Backspace,
        DeleteWordBack,
        DeleteWordForward,
        DeleteLineBack,
        Cut,
        Copy,
        Paste,
        SelectAll,
        SelectNone,
        ScrollStart,
        ScrollEnd,
        ScrollPageUp,
        ScrollPageDown,
    }
}