using GameCharacter.Enemy;
using GamePlay.GameProcess;
using GameSystem.Synthesis;
using Photon.Pun;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace GameCharacter.Minioin.Action
{
    public class MinionAI : Photon.Pun.MonoBehaviourPun, StageChange
    {
        NavMeshAgent nav;
        public GameStageController gameStage;
        public UnityEngine.Object minionInfo;
        float turnSpeed = 20f;
        [SerializeField] float disWithTarget = Mathf.Infinity;
        public GameObject focusTarget;
        [SerializeField] Vector3 readyEndPosition;
        public Animator minionAnimator;
        public MinionState minionState = MinionState.Walk;
        bool isMultipleGame = false;
        public enum MinionState
        {
            Idle,
            Walk,
            Attack,
            Grab,
            StopAction
        }
        void Awake()
        {
            nav = GetComponent<NavMeshAgent>();
            gameStage = FindObjectOfType<GameStageController>();
            gameStage.stageChangeToBattle.AddListener(OnBattleStart);
            gameStage.stageChangeToReady.AddListener(OnReadyStart);
            gameStage.stageChangeToRoundWin.AddListener(OnRoundWin);
            minionAnimator = GetComponent<Animator>();
        }
        void Start()
        {
            ComponentSet();
            AnimationHash();
            enemySpawnSpot = GameObject.Find("EnemySpawn");
            isMultipleGame = PhotonNetwork.IsConnected;
        }
        void Update()
        {
            if (minionsCombat.isMultipleGame) {
                if (!photonView.IsMine) return; }
            if (!nav.hasPath && isBattleMove)
            {
                MinionAC(MinionState.Idle,isMultipleGame);
                isBattleMove = false;
            }
            if (minionState == MinionState.StopAction || minionsCombat.IsSkillOnPlay())
            {
                CancelAttackIfIsAttacking();
                return;
            }
            if (gameStage.GetStageState() == GameStageController.StageState.Battle && !minionsCombat.IsDead())
            {
                Action();
            }
        }

        public void Action()
        {
            if (focusTarget != null && !minionsCombat.IsSkillOnPlay())
            {
                EngageEnemy();
            }
            if ((focusTarget == null || !onFocus) && !IsInvoking("AttackTarget"))
            {
                SearchEnemy();
            }
            if (focusTarget == null)
            {
                CancelAttackIfIsAttacking();
                nav.enabled = false;
            }
        }
        void OnRoundWin()
        {
            if (isActiveAndEnabled)
                CancelAttackIfIsAttacking();

            nav.enabled = false;
            minionAnimator.SetTrigger(animation_Victory);
        }
        public void OnBattleStart()
        {
            if (enabled == false) { return; }
            nav.enabled = true;
            MinionAC(MinionState.Idle,isMultipleGame);
            readyEndPosition = transform.position;
            synthesisHandler.enabled = false;
            minionAnimator.enabled = true;
        }
        public void OnReadyStart()
        {
            if (enabled == false) { return; }

            CancelAttackIfIsAttacking();

            focusTarget = null;
            nav.enabled = false;
            navMeshObstacle.enabled = false;
            minionState = MinionState.Idle;
            gameObject.SetActive(true);
            transform.position = readyEndPosition;
            //多人連線P2轉向180度
            if (isMultipleGame)
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    transform.rotation = Quaternion.AngleAxis(180, Vector3.up);
                }
                else
                    transform.rotation = Quaternion.identity;
            }
            else
                transform.rotation = Quaternion.identity;
            minionAnimator.enabled = true;
            MinionAC(MinionState.Idle,isMultipleGame);
        }

        SynthesisHandler synthesisHandler;
        NavMeshObstacle navMeshObstacle;
        MinionsCombat minionsCombat;
        void ComponentSet()
        {
            synthesisHandler = GetComponent<SynthesisHandler>();
            navMeshObstacle = GetComponent<NavMeshObstacle>();
            minionsCombat = GetComponent<MinionsCombat>();
        }
        float attackSpeed = 0, preAttackSpeed = 0;
        public void EngageEnemy()
        {
            FaceToTarget();
            if (minionsCombat.IsSkillOnPlay()) { return; }
            float attackRange = minionsCombat.GetAttackRange();
            preAttackSpeed = attackSpeed;
            attackSpeed = minionsCombat.GetIndividualAttackSpeed();
            disWithTarget = Vector3.Distance(transform.position, focusTarget.transform.position);
            //與目標小於攻擊距離攻擊
            if (disWithTarget <= attackRange)
            {
                if (preAttackSpeed != attackSpeed)
                {
                    ResetAttack();
                    return;
                }
                if (IsInvoking("AttackTarget"))
                {
                    return;
                }
                nav.enabled = false;
                navMeshObstacle.enabled = true;

                InvokeRepeating("AttackTarget", 0f, attackSpeed);
            }
            else
            {
                CancelAttackIfIsAttacking();

                if (nav.enabled)
                    nav.SetDestination(focusTarget.transform.position);

                minionState = MinionState.Walk;
                MinionAC(MinionState.Walk,isMultipleGame);
            }
        }
        void CancelAttackIfIsAttacking()
        {
            if (IsInvoking("AttackTarget"))
            {
                CancelInvoke("AttackTarget");
                return;
            }
        }
        IEnumerator EnableNavMeshAgain()
        {
            navMeshObstacle.enabled = false;
            yield return null;
            if (!navMeshObstacle.enabled)
            {
                nav.enabled = true;
            }
        }
        IEnumerator EnableNavMeshAgain(Vector3 des)
        {
            navMeshObstacle.enabled = false;
            yield return null;
            if (!navMeshObstacle.enabled)
            {
                nav.enabled = true;
            }
            nav.SetDestination(des);
            minionState = MinionState.StopAction;
            MinionAC(MinionState.Walk,isMultipleGame);
        }
        [SerializeField] bool isBattleMove = false; //戰鬥階段拖曳移動
        public void WalkToPosition(Vector3 des)
        {
            if (minionsCombat.IsSkillOnPlay()) { return; }
            isBattleMove = true;
            if (nav.enabled == false)
            {
                StartCoroutine(EnableNavMeshAgain(des));
            }
            else
            {
                nav.SetDestination(des);
                minionState = MinionState.StopAction;
                MinionAC(MinionState.Walk,isMultipleGame);
            }
        }
        [SerializeField] float faceAngle;
        public void AttackTarget()
        {
            if (focusTarget == null) { return; }
            if (focusTarget.GetComponent<EnemyCombat>().isDead) { focusTarget = null; return; }

            faceAngle = Vector3.Angle(focusTarget.transform.position - transform.position, transform.forward);
            minionState = MinionState.Attack;

            if (Mathf.Abs(faceAngle) <= 8)//面對目標角度小於8度才攻擊
            {
                if (!minionsCombat.IsSkillOnPlay() && minionsCombat.skillQueue.Count > 0)
                {
                    //print(gameObject.name + " Skill Count : " + minionsCombat.skillQueue.Count);
                    minionAnimator.SetBool("Run", false);
                    minionsCombat.IsSkillOnPlay(true);
                    minionsCombat.skillQueue.Dequeue().Invoke();
                }
                else if (!minionsCombat.IsSkillOnPlay())
                {
                    //判斷特殊狀態 "停止攻擊"
                    foreach (SpecialState specialState in minionsCombat.specialStateList)
                    {
                        if (specialState == SpecialState.Prohibit)
                        {
                            return;
                        }
                    }

                    if (attackSpeed < 1) //改變動畫速度
                    {
                        minionAnimator.SetFloat(animation_AttackSpeedMultiplier, 1 + (1 - attackSpeed));
                    }
                    MinionAC(MinionState.Attack,isMultipleGame);
                }
            }
        }
        public void ResetAttack()
        {
            if (minionState == MinionState.StopAction) { return; }
            if (IsInvoking("AttackTarget"))
            {
                CancelInvoke("AttackTarget");
                InvokeRepeating("AttackTarget", 0.01f, attackSpeed);
            }
        }
        [SerializeField] bool onFocus = false;
        Coroutine changeFocusCoroutine;
        //被手動改變目標呼叫
        public void ChangeFocus(GameObject enemy)
        {
            if (minionsCombat.isSkillOnPlay) { return; }
            CancelAttackIfIsAttacking();

            focusTarget = enemy;
            onFocus = true;
            minionState = MinionState.Idle;
            if (changeFocusCoroutine == null)
            {
                changeFocusCoroutine = StartCoroutine(ChangeFocusCoroutine());
            }
        }
        IEnumerator ChangeFocusCoroutine()
        {
            yield return new WaitForSeconds(attackSpeed);
            changeFocusCoroutine = null;
            EngageEnemy();
        }
        public void FaceToTarget()
        {
            if (focusTarget != null)
            {
                Vector3 direction = focusTarget.transform.position - transform.position;
                Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * turnSpeed);
            }
        }
        int animation_Run, animation_Grab, animation_Idle, animation_Attack, animation_Victory, animation_AttackSpeedMultiplier;
        public void AnimationHash()
        {
            animation_Idle = Animator.StringToHash("Idle");
            animation_Run = Animator.StringToHash("Run");
            animation_Grab = Animator.StringToHash("Grab");
            animation_Attack = Animator.StringToHash("Attack");
            animation_Victory = Animator.StringToHash("Victory");
            animation_AttackSpeedMultiplier = Animator.StringToHash("AttackSpeedMultiplier");
        }
        [PunRPC]
        public void MinionAC(MinionState state,bool isMultiple)
        {
            if (isMultiple) {
                GetComponent<PhotonView>().RPC("MinionAC",RpcTarget.All,state,false);
                return;
            }
            if (state == MinionState.Idle)
            {
                minionAnimator.SetBool(animation_Run, false);
                minionAnimator.SetBool(animation_Grab, false);
                minionAnimator.SetTrigger(animation_Idle);
            }
            else if (state == MinionState.Walk)
            {
                minionAnimator.SetBool(animation_Run, true);
            }
            else if (state == MinionState.Grab)
            {
                minionAnimator.SetBool(animation_Grab, true);
            }
            else
            {
                minionAnimator.SetBool(animation_Run, false);
                minionAnimator.SetTrigger(animation_Attack);
            }
        }
        GameObject enemySpawnSpot;
        EnemyCombat[] enemyList;
        public void SearchEnemy()
        {
            enemyList = enemySpawnSpot.GetComponentsInChildren<EnemyCombat>();
            if (enemyList == null) { return; }
            float preDis = Mathf.Infinity;
            GameObject preTarget = focusTarget;
            foreach (EnemyCombat enemy in enemyList)
            {
                disWithTarget = Vector3.Distance(transform.position, enemy.transform.position);
                if (disWithTarget <= preDis && !enemy.isDead)
                {
                    preDis = disWithTarget;
                    focusTarget = enemy.gameObject;
                }
            }
            //轉換目標時導航障礙BUG修正
            if (preTarget == focusTarget)
            {
                if (!nav.enabled)
                    StartCoroutine(EnableNavMeshAgain());
            }
        }
    }
}
