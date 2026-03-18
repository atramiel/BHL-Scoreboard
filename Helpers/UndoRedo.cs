using Scoreboard.ViewModels;
using System.Text.Json;

namespace Scoreboard
{
    public class UndoRedo
    {
        private Stack<string> undoStack = new Stack<string>();
        private Stack<string> redoStack = new Stack<string>();

        public void Cache(MainWindowViewModel details)
        {
            undoStack.Push(JsonSerializer.Serialize(details));
            redoStack.Clear();
        }
        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
        }
        public MainWindowViewModel? Undo(MainWindowViewModel current)
        {
            if (undoStack.Count <= 0)
                return null;

            redoStack.Push(JsonSerializer.Serialize(current));
            return JsonSerializer.Deserialize<MainWindowViewModel>(undoStack.Pop());
        }
        public MainWindowViewModel? Redo(MainWindowViewModel current)
        {
            if (redoStack.Count <= 0)
                return null;

            undoStack.Push(JsonSerializer.Serialize(current));
            return JsonSerializer.Deserialize<MainWindowViewModel>(redoStack.Pop());
        }
    }
}
