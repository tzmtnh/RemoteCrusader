﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour {

    public static Player instance;

    public enum Command
    {
        Left,
        Right
    }
    public delegate void IssueCommand(Command cmd, float delay);
    public static event IssueCommand OnCommand;

    private Queue<Command> commandQueue = new Queue<Command>();

    private static readonly float[] LANE_POSITIONS = new float[] { -1, 0, 1 };
    private int laneIndex = Array.IndexOf(LANE_POSITIONS, 0);

    private float gameStartTime;

    [SerializeField]
    private float timePlayed = 0;

    [SerializeField]
    private float delay = 0.0f;

    [SerializeField]
    private float laneChangeTime = 1.0f;

    [SerializeField]
    private AnimationCurve laneChangePositionCurve;

    [SerializeField]
    private AnimationCurve laneChangeRotationCurve;

    [SerializeField]
    private float laneChangeRotationAngle = -35;

    private bool isChangingLane = false;
    private float laneChangeStartTime;
    private float laneChangeEndTime;
    private Vector3 startPosition;
    private Vector3 targetPosition;

    public int maxHealth = 3;
    public int currentHealth = 3;

    [SerializeField]
    private float amplifyAmount = .1f;

    void Awake()
    {
        instance = this;
    }

    void Start () {
        gameStartTime = Time.time;
        SetLane(this.laneIndex);
        Hitable.onHitableHit += OnHit;
	}

	void Update ()
    {
        timePlayed = Time.time - gameStartTime;
        delay = timePlayed * .01f;

		if (Input.GetKeyDown("left"))
        {
            IssueLeftCommand();
        }
        else if (Input.GetKeyDown("right"))
        {
            IssueRightCommand();
        }

        if (isChangingLane)
        {
            var t = (Time.time - laneChangeStartTime) / (laneChangeEndTime - laneChangeStartTime);
            var movementVector = targetPosition - startPosition;
            var newPosition = startPosition + movementVector * laneChangePositionCurve.Evaluate(t);
            var newRotation = Quaternion.AngleAxis(laneChangeRotationCurve.Evaluate(t) * laneChangeRotationAngle * (movementVector.x / Math.Abs(movementVector.x)), Vector3.forward);
            this.gameObject.transform.position = newPosition;
            this.gameObject.transform.rotation = newRotation;
            
            if (Time.time > laneChangeEndTime)
            {
                isChangingLane = false;
            }
        }
        else
        {
            ReadCommandQueue();
        }
    }

    void ReadCommandQueue()
    {
        if (commandQueue.Count > 0)
        {
            var cmd = commandQueue.Dequeue();
            switch (cmd)
            {
                case Command.Left:
                    MoveLeft();
                    break;
                case Command.Right:
                    MoveRight();
                    break;
            }
        }
    }

    void IssueLeftCommand()
    {
        if (OnCommand != null)
        {
            OnCommand(Command.Left, this.delay);
        }
        StartCoroutine(QueueCommand(Command.Left, this.delay));
    }

    void IssueRightCommand()
    {
        if (OnCommand != null)
        {
            OnCommand(Command.Right, this.delay);
        }
        StartCoroutine(QueueCommand(Command.Right, this.delay));
    }

    IEnumerator QueueCommand(Command cmd, float delay)
    {
        yield return new WaitForSeconds(delay);
        commandQueue.Enqueue(cmd);
    }

    void MoveLeft()
    {
        if (laneIndex > 0 && !isChangingLane)
        {
            this.laneIndex--;
            SetTargetLane(this.laneIndex);
        }
    }

    void MoveRight()
    {
        if (laneIndex < LANE_POSITIONS.Length - 1 && !isChangingLane)
        {
            this.laneIndex++;
            SetTargetLane(this.laneIndex);
        }
    }

    void SetLane(int laneIndex)
    {
        var pos = gameObject.transform.position;
        gameObject.transform.position = new Vector3(LANE_POSITIONS[laneIndex], pos.y, pos.z);
    }

    void SetTargetLane(int laneIndex)
    {
        var pos = gameObject.transform.position;
        this.laneChangeStartTime = Time.time;
        this.laneChangeEndTime = this.laneChangeStartTime + this.laneChangeTime;
        this.startPosition = pos;
        this.targetPosition = new Vector3(LANE_POSITIONS[laneIndex], pos.y, pos.z);
        this.isChangingLane = true;
    }

    void OnHit(Hitable.HitableType hitType)
    {
        switch (hitType)
        {
            case Hitable.HitableType.Astroid:
                if (currentHealth > 0)
                {
                    currentHealth--;
                }
                break;
            case Hitable.HitableType.Repair:
                if (currentHealth < this.maxHealth)
                {
                    currentHealth++;
                }
                break;
            case Hitable.HitableType.Amplify:
                delay = Mathf.Max(0, delay - amplifyAmount);
                break;
            case Hitable.HitableType.Jump:
                break;
        }
    }
}
