using UnityEngine;

public class CursorLockManager : MonoBehaviour
{
    void Start()
    {
        // 游戏启动时隐藏并锁定鼠标到屏幕中心
        LockCursor();
    }

    void Update()
    {
        // 按 ESC 释放鼠标
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }

        // 【可选】如果鼠标是释放状态，玩家点击左键时重新锁定（类似MC/CSGO的逻辑）
        if (Cursor.lockState == CursorLockMode.None && Input.GetMouseButtonDown(0))
        {
            LockCursor();
        }
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;  // 锁定在中心
        Cursor.visible = false;                   // 隐藏图标
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;   // 释放
        Cursor.visible = true;                   // 显示图标
    }
}