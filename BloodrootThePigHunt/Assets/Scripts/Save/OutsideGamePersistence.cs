using System;
using System.IO;
using UnityEngine;

public class PersistOutsideWorld : MonoBehaviour
{
    public static PersistOutsideWorld instance;
    private StoreOutsideGame saveSystem;
    public bool state;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // 1. Instantiate the helper class
        saveSystem = new StoreOutsideGame();

        // 2. Load safely
        LoadData();
    }

    public virtual void SaveData(bool newGame)
    {
        state = newGame;
        if (saveSystem == null) saveSystem = new StoreOutsideGame();
        saveSystem.WriteBinary(state);
    }

    public virtual void LoadData()
    {
        if (saveSystem == null) saveSystem = new StoreOutsideGame();
        state = saveSystem.ReadBinary();
    }
}

public class StoreOutsideGame
{
    // Use .bin or .dat instead of .json since this is writing raw binary data
    private readonly string savePath = Path.Combine(Application.persistentDataPath, "systemSave.bin");

    public void WriteBinary(bool newGame)
    {
        try
        {
            using (FileStream stream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(newGame);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Failed to write save file: {ex.Message}");
        }
    }

    public bool ReadBinary()
    {
        if (!File.Exists(savePath))
        {
            // Default fallback if no save exists yet
            return false;
        }

        try
        {
            using (FileStream stream = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                return reader.ReadBoolean();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Failed to read save file: {ex.Message}");
            return false;
        }
    }
}