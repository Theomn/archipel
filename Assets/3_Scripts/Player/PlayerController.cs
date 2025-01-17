using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : SingletonMonoBehaviour<PlayerController>
{
    [SerializeField] private float speed;
    [SerializeField] private float jumpForce;
    [SerializeField] private float gravity;
    [SerializeField] private float footstepInterval;
    [SerializeField] private Transform visual;
    [SerializeField] private PlayerAnimation anim;
    [SerializeField] ParticleSystem dustParticles, rippleParticles;
    [SerializeField] public Transform head;
    public Transform cameraTarget, sitCameraTarget;

    [Header("Wwise")]
    [SerializeField] private AK.Wwise.Event jumpUpEvent;
    [SerializeField] private AK.Wwise.Event jumpDownEvent, walkEvent, sitDownEvent, sitUpEvent;


    public enum State
    {
        Walking,
        Idle,
        Jumping,
        Falling,
        Sitting,
        Start
    }

    public State state { get; private set; }
    private bool isPaused;
    private bool unpauseFlag;
    private bool isSpeedCheat;
    private bool isGrounded;
    private bool isOnWater;
    private bool isFrozen;
    public bool isInside { get; private set; }
    private float initialSpeed;

    // used to grab/drop objects
    public Vector3 forward { get; private set; }
    private Vector3 input;

    private Rigidbody rb;

    private Localization loc;
    private HUDController hud;
    private DiaryScreen diaryScreen;

    private float footstepTimer;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        initialSpeed = speed;
        footstepTimer = footstepInterval;
    }

    private void Start()
    {
        loc = GameController.instance.localization;
        hud = HUDController.instance;
        hud.sit.Show(false);
        diaryScreen = DiaryScreen.instance;
        if (diaryScreen) hud.diary.Show(diaryScreen.DiaryIsAccessible(), diaryScreen.DiaryIsAccessible() ? diaryScreen.buttonDiary : "");
        //hud.jump?.Show(true, loc.GetText("action_jump"));
    }

    void Update()
    {
        if (isFrozen) return;
        if (isPaused)
        {
            if (unpauseFlag)
            {
                isPaused = false;
                unpauseFlag = false;
            }
            return;
        }
        if (state == State.Sitting)
        {
            if (Input.GetButtonDown(ButtonName.sit) || 
                Input.GetButtonDown(ButtonName.jump) || 
                Input.GetButtonDown(ButtonName.use) || 
                Input.GetButtonDown(ButtonName.grab) || 
                Input.GetButtonDown(ButtonName.cancel))
            {
                LeaveSitting();
                SetIdle();
            }
            return;
        }

        if (Input.GetButtonDown(ButtonName.cancel))
        {
            ControlToggle.TakeControlLimited(PauseMenu.instance.Close);
            PauseMenu.instance.Open();
        }

        if (state == State.Start)
        {
            if (input.sqrMagnitude > 0)
            {
                hud.ShowInputs(true);
                SetWalking();
            }
        }

        // Input
        var horizontal = Input.GetAxis("Horizontal");
        var vertical = Input.GetAxis("Vertical");
        input = Vector3.forward * vertical + Vector3.right * horizontal;
        if (input.sqrMagnitude > 1)
        {
            input.Normalize();
        }
        if (input.sqrMagnitude > 0)
        {
            forward = input.normalized;
        }
        anim.SetInput(input);

        // Move
        var velocity = new Vector3(input.x * speed, rb.velocity.y, input.z * speed);
        rb.velocity = velocity;

        // Ground stuff
        if (state == State.Walking)
        {
            anim.Walk();
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0)
            {
                Step();
                footstepTimer = footstepInterval;
            }
            if (rb.velocity.sqrMagnitude < 0.1f)
            {
                SetIdle();
            }
        }

        if (state == State.Walking || state == State.Idle)
        {
            if (Input.GetButtonDown("Sit") && !PlayerItem.instance.isHoldingItem && ThoughtScreen.instance.ThoughtCount() > 0)
            {
                SetSitting();
            }
        }

        if (state == State.Idle)
        {
            if (input.sqrMagnitude > 0)
            {
                SetWalking();
            }
        }

        // Air stuff
        if (state == State.Walking || state == State.Idle)
        {
            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                SetJumping();
            }
            else if (!isGrounded)
            {
                SetFalling();
            }
        }

        if (state == State.Jumping)
        {

            if (rb.velocity.y < 0)
            {
                SetFalling();
            }
        }

        if (state == State.Falling)
        {
            if (isGrounded)
            {
                jumpDownEvent.Post(gameObject);
                SetIdle();
            }
        }

    }

    private void FixedUpdate()
    {
        // Groundcheck
        isGrounded = Physics.CheckSphere(transform.position + Vector3.down * 0.1f, 0.2f, 1 << Layer.ground);
        if (!isFrozen)
        {
            rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
        }

        if (isGrounded)
        {
            if (!isOnWater && !dustParticles.isEmitting)
            {
                rippleParticles.Stop();
                dustParticles.Play();
            }
            else if (isOnWater && !rippleParticles.isEmitting)
            {
                dustParticles.Stop();
                rippleParticles.Play();
            }
        }
        else if (!isGrounded)
        {
            dustParticles.Stop();
            rippleParticles.Stop();
        }

        if (isPaused || state == State.Sitting || input.sqrMagnitude < 0.1f)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
        }
        else
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    public void Pause(bool pause)
    {
        if (pause)
        {
            SetIdle();
            rb.velocity = new Vector3(0, rb.velocity.y, 0);
            isPaused = true;
            unpauseFlag = false;
            hud.sit.Show(false);
            hud.diary.Show(false);
        }
        else
        {
            unpauseFlag = true;
            if (ThoughtScreen.instance.ThoughtCount() > 0)
                hud.sit.Show(true, loc.GetText("action_sit"));
            if (diaryScreen) hud.diary.Show(diaryScreen.DiaryIsAccessible(), diaryScreen.DiaryIsAccessible() ? diaryScreen.buttonDiary : "");
        }
    }

    public void StartState()
    {
        anim.Sit();
        state = State.Start;
        hud.ShowInputs(false);
    }

    public void EndingState()
    {
        Freeze(true);
        anim.SetFacing(3);
        anim.Idle();
    }

    private void SetWalking()
    {
        state = State.Walking;
        footstepTimer = footstepInterval / 2f;
    }

    private void SetIdle()
    {
        state = State.Idle;
        anim.Idle();
    }
    private void SetSitting()
    {
        state = State.Sitting;
        rb.velocity = new Vector3(0, 0, 0);
        anim.Sit();
        sitDownEvent.Post(gameObject);
        ThoughtScreen.instance.Open();
        PlayerItem.instance.Pause(true);
        hud.BackInput(true);
        CameraController.instance.SitZoom(true);
    }

    private void LeaveSitting()
    {
        ThoughtScreen.instance.Close();
        sitUpEvent.Post(gameObject);
        PlayerItem.instance.Pause(false);
        hud.BackInput(false);
        if (ThoughtScreen.instance.ThoughtCount() > 0)
            hud.sit.Show(true, loc.GetText("action_sit"));
        if (diaryScreen) hud.diary.Show(diaryScreen.DiaryIsAccessible(), diaryScreen.DiaryIsAccessible() ? diaryScreen.buttonDiary : "");
        //hud.jump?.Show(true, loc.GetText("action_jump"));
        CameraController.instance.SitZoom(false);
    }
    private void SetJumping()
    {
        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        state = State.Jumping;
        anim.Jump();
        jumpUpEvent.Post(gameObject);
    }
    private void SetFalling()
    {
        state = State.Falling;
        anim.Fall();
    }

    public void Step()
    {
        walkEvent.Post(gameObject);
    }

    public void SetInside(bool active)
    {
        isInside = active;
    }

    public void SetOnWater(bool active)
    {
        isOnWater = active;
    }

    public void Freeze(bool freeze)
    {
        isFrozen = freeze;
        rb.velocity = Vector3.zero;
        rb.isKinematic = freeze;
        GetComponent<Collider>().enabled = !freeze;
    }

    public void ToggleSpeedCheat()
    {
        if (isSpeedCheat)
        {
            speed = initialSpeed;
            isSpeedCheat = false;
        }
        else
        {
            speed = initialSpeed * 2f;
            isSpeedCheat = true;
        }
    }
}
