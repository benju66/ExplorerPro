// Create in Models/Command.cs
using System;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Abstract base class for all undoable commands.
    /// </summary>
    public abstract class Command
    {
        /// <summary>
        /// Execute the command.
        /// </summary>
        public abstract void Execute();

        /// <summary>
        /// Undo the command.
        /// </summary>
        public abstract void Undo();
    }
}