using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    private PlayerController _playerController;

    [Header("设置")]
    public float attackCooldown = 1.5f;
    private float _attackTimer;

    void Awake()
    {
        _playerController = GetComponent<PlayerController>();
    }

    public void UpdateAttack()
    {
   
    }

    public void TryAttack()
    {
 
    }
}