using System.IO;
using UnityEditor;
using UnityEngine;

namespace PipeFlow.EditorTools
{
    public static class PipeFlowResetProgress
    {
        [MenuItem("Pipe Flow/Reset Local Test Progress")]
        public static void Reset()
        {
            if (!EditorUtility.DisplayDialog("Reset test progress",
                "Start from level 1 with zero stars? This deletes local test progress from the current device.",
                "Reset", "Cancel")) return;
            foreach (string fileName in new[] { PipeProgress.SaveFileName, "pipe_flow_native_v44_fix_progress.json" })
            {
                string path = Path.Combine(Application.persistentDataPath, fileName);
                if (File.Exists(path)) File.Delete(path);
            }
            Debug.Log("Pipe Flow: local test progress reset. Stop and restart Play Mode.");
        }
    }
}
