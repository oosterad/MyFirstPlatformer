using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Platformer.Gameplay;
using static Platformer.Core.Simulation;
using Platformer.Model;
using Platformer.Core;

namespace Platformer.Mechanics
{
    /// <summary>
    /// This is the main class used to implement control of the player.
    /// It is a superset of the AnimationController class, but is inlined to allow for any kind of customisation.
    /// </summary>
    public class PlayerController : KinematicObject
    {
        public AudioClip jumpAudio;
        public AudioClip respawnAudio;
        public AudioClip ouchAudio;

        /// <summary>
        /// Max horizontal speed of the player.
        /// </summary>
        public float maxSpeed = 7;
        /// <summary>
        /// Initial jump velocity at the start of a jump.
        /// </summary>
        public float jumpTakeOffSpeed = 7;

        public JumpState jumpState = JumpState.Grounded;
        private bool stopJump;
        /*internal new*/ public Collider2D collider2d;
        /*internal new*/ public AudioSource audioSource;
        public Health health;
        public bool controlEnabled = true;

        bool jump;
        Vector2 move;
        SpriteRenderer spriteRenderer;
        internal Animator animator;
        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        public Bounds Bounds => collider2d.bounds;

        bool isCloseToLadder = false;
        bool isClimbing = false;
        bool hasStartedClimb = false;
        bool climbHeld = false;

         
        private Transform ladder;
        private float vertical = 0f;
        private float climbSpeed = 0.2f;

        private Rigidbody2D rigidBody2D;

        void Awake()
        {
            Console.WriteLine("Awake");

            health = GetComponent<Health>();
            audioSource = GetComponent<AudioSource>();
            collider2d = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
        }

        void Start() {
            rigidBody2D = gameObject.GetComponent<Rigidbody2D>();
        }

        protected override void Update() {
            if (controlEnabled) {
                move.y = Input.GetAxis("Vertical");
                move.x = Input.GetAxis("Horizontal");

                if (move.x > 0.01f || move.x < -0.01f) {
                    Console.WriteLine("y = " + move.y + " and x = " + move.x);
                } else if (move.y > 0.01f || move.y < -0.01f) {
                    Console.WriteLine("y = " + move.y + " and x = " + move.x);
                }


                climbHeld = (isCloseToLadder && Input.GetButton("Vertical")) ? true : false;
 
                if (climbHeld) {
                    hasStartedClimb = true;
                } else {
                    if (hasStartedClimb) {
                        //GetComponent<Animator>().Play("CharacterClimbIdle");
                    }
                }

                if (jumpState == JumpState.Grounded && Input.GetButtonDown("Jump")) {
                    jumpState = JumpState.PrepareToJump;
                } else if (Input.GetButtonUp("Jump")) {
                    stopJump = true;
                    Schedule<PlayerStopJump>().player = this;
                }
            } else {
                move.y = 0;
                move.x = 0;
            }
            UpdateJumpState();
            base.Update();
        }

        private void OnTriggerStay2D(Collider2D collision) {
            if (collision.gameObject.tag.Equals("Ladder")) {
                Console.WriteLine("Trigger Ladder");

                isCloseToLadder = true;
                this.ladder = collision.transform;
            }
        }

        private void OnTriggerExit2D(Collider2D collision) {
            if (collision.gameObject.tag.Equals("Ladder")) {
                Console.WriteLine("TriggerExit Ladder");

                isCloseToLadder = false;
                ResetClimbing();
            }
        }

        void FixedUpdate2() {
            // Climbing
            if (hasStartedClimb && !climbHeld) {
                if (move.x > 0 || move.x < 0) {
                    ResetClimbing();
                }
            } else if (hasStartedClimb && climbHeld) {
                float height         = GetComponent<SpriteRenderer>().size.y;
                float topHandlerY    = Half(ladder.transform.GetChild(0).transform.position.y + height);
                float bottomHandlerY = Half(ladder.transform.GetChild(1).transform.position.y + height);
                float transformY     = Half(transform.position.y);
                float transformVY    = transformY + vertical;
        
                if (transformVY > topHandlerY || transformVY < bottomHandlerY) {
                    ResetClimbing();
                } else if (transformY <= topHandlerY && transformY >= bottomHandlerY) {
                    rigidBody2D.bodyType = RigidbodyType2D.Kinematic;
                    if (!transform.position.x.Equals(ladder.transform.position.x)) {
                        transform.position = new Vector3(ladder.transform.position.x,transform.position.y,transform.position.z);
                    }
        
                    GetComponent<Animator>().Play("CharacterClimb");
                    Vector3 forwardDirection = new Vector3(0, transformVY, 0); 
                    Vector3 newPos = Vector3.zero;
                    if (vertical > 0) {
                    
                        newPos = transform.position + forwardDirection * Time.deltaTime * climbSpeed;
                    } else if(vertical < 0) {
                        newPos = transform.position - forwardDirection * Time.deltaTime * climbSpeed;
                        if (newPos != Vector3.zero) {
                            rigidBody2D.MovePosition(newPos);
                        }
                    }
                }
            } else {
                base.FixedUpdate();
            }
        }

        private void ResetClimbing() {
            if (hasStartedClimb) {
                hasStartedClimb = false;
                rigidBody2D.bodyType = RigidbodyType2D.Dynamic;
            }
        }

        // Show the number of calls to both messages.
        void OnGUI()
        {
            GUIStyle fontSize = new GUIStyle(GUI.skin.GetStyle("label"));
            fontSize.fontSize = 24;
            GUI.Label(new Rect(100, 50,  250, 50), "isCloseToLadder: " + isCloseToLadder.ToString(), fontSize);
            GUI.Label(new Rect(100, 100, 250, 50), "hasStartedClimb: " + hasStartedClimb.ToString(), fontSize);
            GUI.Label(new Rect(100, 150, 250, 50), "climbHeld: " + climbHeld.ToString(), fontSize);
        }


        public static float Half(float value) {
            return Mathf.Floor(value) + 0.5f;
        }

        void UpdateJumpState() {
            jump = false;
            switch (jumpState)
            {
                case JumpState.PrepareToJump:
                    jumpState = JumpState.Jumping;
                    jump = true;
                    stopJump = false;
                    break;
                case JumpState.Jumping:
                    if (!IsGrounded)
                    {
                        Schedule<PlayerJumped>().player = this;
                        jumpState = JumpState.InFlight;
                    }
                    break;
                case JumpState.InFlight:
                    if (IsGrounded)
                    {
                        Schedule<PlayerLanded>().player = this;
                        jumpState = JumpState.Landed;
                    }
                    break;
                case JumpState.Landed:
                    jumpState = JumpState.Grounded;
                    break;
            }
        }

        protected override void ComputeVelocity()
        {
            if (jump && IsGrounded) {
                velocity.y = jumpTakeOffSpeed * model.jumpModifier;
                jump = false;
            } else if (stopJump) {
                stopJump = false;
                if (velocity.y > 0) {
                    velocity.y = velocity.y * model.jumpDeceleration;
                }
            } else if (climbHeld) {
                 velocity.y = jumpTakeOffSpeed * model.jumpModifier;
            } else {
                if (velocity.y > 0) {
                    velocity.y = velocity.y * model.jumpDeceleration;
                }
            }

            if (move.x > 0.01f) {
                spriteRenderer.flipX = false;
            } else if (move.x < -0.01f) {
                spriteRenderer.flipX = true;
            }

            animator.SetBool("grounded", IsGrounded);
            animator.SetFloat("velocityX", Mathf.Abs(velocity.x) / maxSpeed);
            
            targetVelocity = move * maxSpeed;
        }

        public enum JumpState
        {
            Grounded,
            PrepareToJump,
            Jumping,
            InFlight,
            Landed
        }
    }
}