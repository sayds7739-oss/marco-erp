using System;
using System.Collections.Generic;

namespace MarcoERP.WpfUI.Common
{
    /// <summary>
    /// Represents a single undoable/redoable action.
    /// </summary>
    public sealed class UndoAction
    {
        public string PropertyName { get; }
        public object OldValue { get; }
        public object NewValue { get; }
        public Action<object> ApplyValue { get; }

        public UndoAction(string propertyName, object oldValue, object newValue, Action<object> applyValue)
        {
            PropertyName = propertyName;
            OldValue = oldValue;
            NewValue = newValue;
            ApplyValue = applyValue ?? throw new ArgumentNullException(nameof(applyValue));
        }
    }

    /// <summary>
    /// Generic undo/redo stack manager.
    /// Max depth: 50 actions. Redo stack is cleared on new user action.
    /// </summary>
    public sealed class UndoRedoManager
    {
        private const int MaxDepth = 50;

        private readonly Stack<UndoAction> _undoStack = new();
        private readonly Stack<UndoAction> _redoStack = new();

        /// <summary>Suppresses recording when applying undo/redo values.</summary>
        internal bool IsSuppressed { get; private set; }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public event EventHandler StateChanged;

        /// <summary>
        /// Records a property change for undo support.
        /// Call this AFTER the property has been set.
        /// </summary>
        public void RecordChange(string propertyName, object oldValue, object newValue, Action<object> applyValue)
        {
            if (IsSuppressed) return;

            _undoStack.Push(new UndoAction(propertyName, oldValue, newValue, applyValue));
            _redoStack.Clear();

            // Trim if over max depth
            if (_undoStack.Count > MaxDepth)
            {
                var temp = new Stack<UndoAction>();
                int kept = 0;
                foreach (var item in _undoStack)
                {
                    if (kept >= MaxDepth) break;
                    temp.Push(item);
                    kept++;
                }
                _undoStack.Clear();
                foreach (var item in temp)
                    _undoStack.Push(item);
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Undoes the last recorded change.
        /// </summary>
        public void Undo()
        {
            if (_undoStack.Count == 0) return;

            var action = _undoStack.Pop();
            IsSuppressed = true;
            try
            {
                action.ApplyValue(action.OldValue);
            }
            finally
            {
                IsSuppressed = false;
            }
            _redoStack.Push(action);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Redoes the last undone change.
        /// </summary>
        public void Redo()
        {
            if (_redoStack.Count == 0) return;

            var action = _redoStack.Pop();
            IsSuppressed = true;
            try
            {
                action.ApplyValue(action.NewValue);
            }
            finally
            {
                IsSuppressed = false;
            }
            _undoStack.Push(action);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clears both stacks (e.g., after save or load).
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
