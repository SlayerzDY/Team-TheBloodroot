//==============================================================================================
// Using Unity Engine
//==============================================================================================
using System.IO;
using UnityEngine;
//==============================================================================================
// Declare Save System
//==============================================================================================
public static class SaveSystem {
    //==========================================================================================
    // Declare Variables
    //==========================================================================================
    private static string savePath = Path.Combine(Application.persistentDataPath, "gamesave.json");
    //==========================================================================================
    // Declare Functions
    //==========================================================================================
    // Function, Save Game
    //------------------------------------------------------------------------------------------
    public static void SaveGame(GameData data) {
        // Convert the C# object into a JSON string
        string json = JsonUtility.ToJson(data, true);
        // Write the string to a text file
        File.WriteAllText(savePath, json);
        //Debug.Log("Game saved to: " + savePath);
    }
    //==========================================================================================
    // Function, Save Game
    //------------------------------------------------------------------------------------------
    // Read data from disk
    public static GameData LoadGame() {
        // Check if a save file actually exists first
        if (!File.Exists(savePath)) {
            //Debug.LogWarning("No save file found. Generating new game state.");
            return new GameData(); // Return default values
        }
        // Read the file text
        string json = File.ReadAllText(savePath);
        // Reconstruct the GameData object from the text string
        GameData data = JsonUtility.FromJson<GameData>(json);
        //Debug.Log("Game successfully loaded.");
        return data;
    }
    //==========================================================================================
}
//==============================================================================================
// Declare Save System
//==============================================================================================