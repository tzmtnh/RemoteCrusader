﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Player : MonoBehaviour {

    public static Player instance;

	public static event Action onPlayerDied;

    public enum Command
    {
        Left,
        Right
    }
    public delegate void IssueCommand(Command cmd, float delay);
    public static event IssueCommand OnCommand;

    private static readonly float[] LANE_POSITIONS = new float[] { -1, 0, 1 };
    private int laneIndex = Array.IndexOf(LANE_POSITIONS, 0);

	private float _speed;
	private float _targetSpeed;
	private float _speedVelocity; // used by SmoothDamp
	public float speed { get { return _speed; } }
	public float normalizedSpeed { get { return _speed / defaultSpeed; } }
	public float lightSpeedParam { get { return Mathf.InverseLerp(defaultSpeed, jumpSpeed, _speed); } }

    public float distance = 0;
	public float duration = 0;
    public float delay = 0.0f;

    [SerializeField]
    private float laneChangeTime = 1.0f;

    [SerializeField]
    private float laneChangeMaxYaw = 35;

    [SerializeField]
    private float laneChangeYawIntensity = 10;

    Vector3 velocity = Vector3.zero;
    private Vector3 targetPosition;

    public int maxHealth = 3;
    public int currentHealth = 0;
	public bool isAlive { get { return currentHealth > 0; } }

    public Texture fullHealthTexture;
    public Texture damageTexture1;
    public Texture damageTexture2;

    public GameObject damageEffect;
    public ParticleSystem damageParticles;
    public GameObject deathExplosion;
    private SpriteRenderer _deathExplosionRenderer;
    public ParticleSystem deathParticles;

    private GameObject shipContainer;

    [SerializeField]
    private float defaultSpeed = 10.0f;

    [SerializeField]
    private float jumpSpeed = 15.0f;

    [SerializeField]
    private float jumpSpeedDuration = 3.0f;

    public float amplifyAmount = .25f;

	bool _canTakeDamage = true;
    public bool canTakeDamage { get { return _canTakeDamage && normalizedSpeed < 1.04f; } }

	Material _shipMaterial;
	int _GlowID;
	float _glow = 0;
	float _glowTarget = 0;
	float _glowVelocity = 0;

	void Awake()
    {
        instance = this;
        shipContainer = GameObject.Find("Spaceship_container");
    }

    void Start () {
		_speed = defaultSpeed;
		_targetSpeed = defaultSpeed;

        Transform ship = shipContainer.transform.Find("Spaceship");
		Assert.IsNotNull(ship);
		_shipMaterial = ship.GetComponent<Renderer>().material;
		_GlowID = Shader.PropertyToID("_Glow");

        _deathExplosionRenderer = deathExplosion.GetComponent<SpriteRenderer>();
        _deathExplosionRenderer.enabled = false;

        ResetToMiddleLane();
        Hitable.onHitableHit += OnHit;
    }

	void Update ()
    {
        if (GameManager.inst.state != GameManager.GameState.InGame)
            return;

		GameManager.GameplayParams par = GameManager.inst.currentParams;

		float dt = Time.deltaTime;
        delay += dt * normalizedSpeed * par.delayPerUnitOfDistance;
        distance += dt * speed;
		duration += dt;

		const float SPEED_SMOOTH_TIME = 1;
		_speed = Mathf.SmoothDamp(_speed, _targetSpeed, ref _speedVelocity, SPEED_SMOOTH_TIME);

		if (InputManager.inst.left)
        {
			AudioManager.inst.playSound("Click");
			IssueLeftCommand();
        }
        else if (InputManager.inst.right)
        {
			AudioManager.inst.playSound("Click");
			IssueRightCommand();
        }

        gameObject.transform.position = Vector3.SmoothDamp(gameObject.transform.position, targetPosition, ref velocity, laneChangeTime);
        gameObject.transform.rotation = Quaternion.AngleAxis(Mathf.Clamp(-velocity.x * laneChangeYawIntensity, -laneChangeMaxYaw, laneChangeMaxYaw), Vector3.forward);

		_glowTarget = canTakeDamage ? 0 : 1;
		_glow = Mathf.SmoothDamp(_glow, _glowTarget, ref _glowVelocity, 0.2f);
		_shipMaterial.SetFloat(_GlowID, _glow);
	}

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(targetPosition, .1f);
    }

    void IssueLeftCommand()
    {
        if (OnCommand != null)
        {
            OnCommand(Command.Left, this.delay);
        }
        Invoke("MoveLeft", this.delay);
    }

    void IssueRightCommand()
    {
        if (OnCommand != null)
        {
            OnCommand(Command.Right, this.delay);
        }
        Invoke("MoveRight", this.delay);
    }

    void MoveLeft()
    {
        if (this.laneIndex > 0)
        {
            SetTargetLane(--this.laneIndex);
			AudioManager.inst.playSound("Change Lane");
        }
    }

    void MoveRight()
    {
        if (this.laneIndex < LANE_POSITIONS.Length - 1)
        {
            SetTargetLane(++this.laneIndex);
			AudioManager.inst.playSound("Change Lane");
		}
    }

    void ResetToMiddleLane()
    {
        this.laneIndex = Array.IndexOf(LANE_POSITIONS, 0);
        var pos = this.gameObject.transform.position;
        gameObject.transform.position = new Vector3(LANE_POSITIONS[this.laneIndex], pos.y, pos.z);
        SetTargetLane(this.laneIndex);
    }

    void SetTargetLane(int laneIndex)
    {
        var pos = this.gameObject.transform.position;
        this.targetPosition = new Vector3(LANE_POSITIONS[laneIndex], pos.y, pos.z);
    }

	Coroutine _canTakeDamageAgainCo;
	void canTakeDamageAgain(float delay) {
		if (_canTakeDamageAgainCo != null) {
			StopCoroutine(_canTakeDamageAgainCo);
		}
		_canTakeDamageAgainCo = StartCoroutine(canTakeDamageAgainCo(delay));
	}

	IEnumerator canTakeDamageAgainCo(float delay) {
		yield return new WaitForSeconds(delay);
		_canTakeDamage = true;
		_canTakeDamageAgainCo = null;
	}

	void OnHit(Hitable.HitableType hitType)
    {
        switch (hitType)
        {
            case Hitable.HitableType.Astroid:
                if (isAlive)
                {
                    SetTexture(--currentHealth);

                    if (currentHealth > 0)
                    {
                        damageParticles.Play();
                        _canTakeDamage = false;

						const float SHIELD_TIME = 3;
                        canTakeDamageAgain(SHIELD_TIME);

						const float SHAKE_TIME = 0.3f;
						CameraControl.inst.shake(SHAKE_TIME);

						AudioManager.inst.playSound("Invulnerability");
                    }
                    else
                    {
                        Explode();
                    }
                    
                }
                break;
            case Hitable.HitableType.Repair:
                if (currentHealth < maxHealth) {
                    SetTexture(++currentHealth);
                }
				UIManager.inst.showPowerup("Repair");
				break;
            case Hitable.HitableType.Amplify:
				delay = Mathf.Max(0, delay - amplifyAmount);
				UIManager.inst.showPowerup("Amplify");
				break;
            case Hitable.HitableType.Jump:
                _targetSpeed = jumpSpeed;
                StartCoroutine(RevertToDefaultSpeed(jumpSpeedDuration));
				UIManager.inst.showPowerup("Light Speed");
                break;
        }
    }

    IEnumerator RevertToDefaultSpeed(float duration)
    {
        yield return new WaitForSeconds(duration);
        _targetSpeed = defaultSpeed;
	}

    private void SetTexture(int health) {
        switch (currentHealth)
        {
            case 3:
                damageEffect.SetActive(false);
                _shipMaterial.mainTexture = fullHealthTexture;
                break;
            case 2:
                damageEffect.SetActive(false);
                _shipMaterial.mainTexture = damageTexture1;
                break;
            case 1:
                damageEffect.SetActive(true);
				_shipMaterial.mainTexture = damageTexture2;
                break;
        }
    }

    void Explode()
    {
        _deathExplosionRenderer.enabled = true;
        this.shipContainer.SetActive(false);
        deathParticles.Play();
        Invoke("EndExplosion", .4f);

		const float SHAKE_TIME = 1f;
		CameraControl.inst.shake(SHAKE_TIME);

		if (onPlayerDied != null)
			onPlayerDied();
	}

    void EndExplosion()
    {
        _deathExplosionRenderer.enabled = false;
    }

    public void Reset()
    {
		StopAllCoroutines();

		distance = 0;
		duration = 0;
		delay = 0;
        currentHealth = maxHealth;
        _canTakeDamage = true;

		_speed = defaultSpeed;
		_targetSpeed = defaultSpeed;
		_speedVelocity = 0;

		_glowTarget = 0;
		_glow = 0;
		_glowVelocity = 0;
		_shipMaterial.SetFloat(_GlowID, _glow);

		shipContainer.SetActive(true);
        ResetToMiddleLane();
        SetTexture(this.currentHealth);

		transform.position = targetPosition;
		transform.rotation = Quaternion.AngleAxis(0, Vector3.forward);
	}
}
