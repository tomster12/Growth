﻿
using System;
using UnityEngine;


[Serializable]
public class Interaction
{
    public enum Visibility { HIDDEN, INPUT, ICON, TEXT }

    public bool IsEnabled { get; protected set; }
    public bool IsActive { get; protected set; }
    public bool IsBlocked { get; protected set; }
    public String Name { get; private set; }
    public InteractionInput Input { get; private set; }
    public Visibility VisibilityState { get; protected set; }

    private Sprite blockedSprite;
    private Sprite spriteInputInactive;
    private Sprite spriteInputActive;
    private Sprite spriteIconInactive;
    private Sprite spriteIconActive;


    public Interaction(string name, InteractionInput input, Visibility visibility, String iconSpriteName)
    {
        IsEnabled = true;
        IsActive = false;
        IsBlocked = false;

        this.Name = name;
        this.Input = input;
        this.VisibilityState = visibility;

        blockedSprite = SpriteSet.Instance.GetSprite("cross");
        spriteInputInactive = SpriteSet.Instance.GetSprite(this.Input.name + "_inactive");
        spriteInputActive  = SpriteSet.Instance.GetSprite(this.Input.name + "_active");
        if (iconSpriteName != null)
        {
            spriteIconInactive = SpriteSet.Instance.GetSprite(iconSpriteName + "_inactive");
            spriteIconActive = SpriteSet.Instance.GetSprite(iconSpriteName + "_active");
        }
    }


    public bool PollInput(IInteractor IInteractor)
    {
        if (!IsEnabled) return false;
        else if (Input.CheckInputDown()) OnInputDown(IInteractor);
        else if (Input.CheckInput()) OnHold(IInteractor);
        else if (Input.CheckInputUp()) OnInputUp(IInteractor);
        else return false;
        return true;
    }

    public Sprite GetCurrentSpriteInput()
    {
        if (IsBlocked) return blockedSprite;
        if (!IsActive) return spriteInputInactive;
        return spriteInputActive;
    }

    public Sprite GetCurrentSpriteIcon()
    {
        if (IsBlocked) return blockedSprite;
        if (!IsActive) return spriteIconInactive;
        return spriteIconActive;
    }


    protected virtual void OnHold(IInteractor IObjectController) { }

    protected virtual void OnInputDown(IInteractor IObjectController) { }

    protected virtual void OnInputUp(IInteractor IObjectController) { }
}
