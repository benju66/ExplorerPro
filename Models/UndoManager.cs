using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ExplorerPro.FileOperations;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Manages command execution and undo/redo functionality.
    /// </summary>
    public class UndoManager
    {
        // Add singleton instance
        private static UndoManager? _instance;
        
        /// <summary>
        /// Gets the singleton instance of UndoManager.
        /// </summary>
        public static UndoManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new UndoManager();
                }
                return _instance;
            }
        }

        private readonly Stack<Command> _undoStack = new Stack<Command>();
        private readonly Stack<Command> _redoStack = new Stack<Command>();
        private readonly ILogger<UndoManager>? _logger;

        /// <summary>
        /// Creates a new instance of UndoManager.
        /// </summary>
        /// <param name="logger">Logger for operation tracking.</param>
        public UndoManager(ILogger<UndoManager>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Executes a command and adds it to the undo stack.
        /// </summary>
        /// <param name="command">Command to execute.</param>
        public void ExecuteCommand(Command command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            _logger?.LogInformation($"Executing command: {command.GetType().Name}");
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear(); // Clear redo stack when new command is executed
        }

        /// <summary>
        /// Undoes the last executed command.
        /// </summary>
        /// <returns>True if a command was undone, false if no commands to undo.</returns>
        public bool Undo()
        {
            if (_undoStack.Count == 0)
            {
                _logger?.LogInformation("No commands to undo");
                return false;
            }

            Command command = _undoStack.Pop();
            _logger?.LogInformation($"Undoing command: {command.GetType().Name}");
            command.Undo();
            _redoStack.Push(command);
            return true;
        }

        /// <summary>
        /// Redoes the last undone command.
        /// </summary>
        /// <returns>True if a command was redone, false if no commands to redo.</returns>
        public bool Redo()
        {
            if (_redoStack.Count == 0)
            {
                _logger?.LogInformation("No commands to redo");
                return false;
            }

            Command command = _redoStack.Pop();
            _logger?.LogInformation($"Redoing command: {command.GetType().Name}");
            command.Execute();
            _undoStack.Push(command);
            return true;
        }

        /// <summary>
        /// Checks if there are commands that can be undone.
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Checks if there are commands that can be redone.
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Clears all undo and redo history.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _logger?.LogInformation("Undo/redo history cleared");
        }
    }
}