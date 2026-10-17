using System.Collections.Generic;

namespace Game.Runtime.Gameplay.Levels
{
    // Explicit priority shared by keyboard and UI. Lower value wins; ties keep the first request.
    public enum LevelPlayCommand { Reset, Undo, Cancel, Pause, Clone, Recycle, Iterate, Up, Right, Down, Left, Select }
    public struct LevelPlayRequest
    {
        public LevelPlayCommand Command;
        public int X, Y;
        public LevelPlayRequest(LevelPlayCommand command, int x = 0, int y = 0) { Command = command; X = x; Y = y; }
    }

    /// <summary>One decision per input frame, independent of Unity callback ordering.</summary>
    public sealed class LevelPlayController
    {
        public LevelPlaySession Session { get; }
        public bool Paused { get; private set; }
        private int lastFrame = -1;
        public LevelPlayController(LevelPlaySession session) { Session = session; }

        public bool ProcessFrame(int frame, IList<LevelPlayRequest> requests, out string message)
        {
            message = "";
            if (frame == lastFrame) return false;
            lastFrame = frame;
            int selected = -1;
            for (int i = 0; i < requests.Count; i++)
                if (Allowed(requests[i].Command) && (selected < 0 || requests[i].Command < requests[selected].Command)) selected = i;
            if (selected < 0) return false;
            var request = requests[selected];
            switch (request.Command)
            {
                case LevelPlayCommand.Reset: Session.Reset(); message = "Level reset."; return true;
                case LevelPlayCommand.Undo: return Session.TryUndo(out message);
                case LevelPlayCommand.Cancel:
                    if (Paused) { Paused = false; message = "Resumed."; }
                    else { Session.CancelSelection(); message = "Selection cancelled."; }
                    return true;
                case LevelPlayCommand.Pause:
                    Session.CancelSelection(); Paused = !Paused; message = Paused ? "Paused." : "Resumed."; return true;
                case LevelPlayCommand.Clone: return Session.TryBeginClonePlacement(out message);
                case LevelPlayCommand.Recycle: return Session.TryBeginRecycling(out message);
                case LevelPlayCommand.Iterate: return Session.TryIterate(out message);
                case LevelPlayCommand.Up: return Session.TryMove(0, 1, out message);
                case LevelPlayCommand.Right: return Session.TryMove(1, 0, out message);
                case LevelPlayCommand.Down: return Session.TryMove(0, -1, out message);
                case LevelPlayCommand.Left: return Session.TryMove(-1, 0, out message);
                case LevelPlayCommand.Select:
                    return Session.Mode == LevelInteractionMode.PlaceClone
                        ? Session.TryCreateClone(request.X, request.Y, out message)
                        : Session.TryRecycleClone(request.X, request.Y, out message);
                default: return false;
            }
        }

        private bool Allowed(LevelPlayCommand command)
        {
            if (Paused) return command == LevelPlayCommand.Pause || command == LevelPlayCommand.Cancel;
            if (Session.Status != LevelPlayStatus.Playing) return command == LevelPlayCommand.Reset || command == LevelPlayCommand.Undo;
            if (command == LevelPlayCommand.Cancel || command == LevelPlayCommand.Pause) return true;
            if (Session.Mode != LevelInteractionMode.Move) return command == LevelPlayCommand.Select;
            return command != LevelPlayCommand.Select;
        }
    }
}
