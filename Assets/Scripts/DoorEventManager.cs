using System;
using UnityEngine;

public class DoorEventManager : MonoBehaviour
{
    
    private Animator _animator;

    private void Start()
    {
        _animator = GetComponent<Animator>();
    }

    private void Open()
    {
        _animator.SetBool("isOpen_Obj_1", true);       
    }

    private void Close()
    {
        _animator.SetBool("isOpen_Obj_1", false);
    }

    public void AutoOpenClose()
    {
        _animator.SetBool("isOpen_Obj_1", true);
        Invoke(nameof(Close), 3f);
    } 
}
