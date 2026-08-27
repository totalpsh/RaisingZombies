using System;
using Spine;
using Spine.Unity;
using UnityEngine;

public class UnitAnimation : MonoBehaviour
{
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    [Header("Animation Names")]
    [SerializeField] private string idleAnimationName = "Idle";
    [SerializeField] private string walkAnimationName = "Walk";
    [SerializeField] private string attackAnimationName = "Attack2";
    [SerializeField] private string hitAnimationName = "Hit";
    [SerializeField] private string dieAnimationName = "Die";
    
    [SerializeField] private bool useHit = true;
    [SerializeField, Range(0f, 1f)] private float attackHitRate = 0.55f;
    
    private TrackEntry _attackTrack;
    private Action _onAttackHit;
    private bool _attackHitDone;
    
    private TrackEntry _deathTrack;
    private bool _isActionPlaying;
    private bool _isDead;

    public bool IsBusy => _isDead || IsOneShotPlaying();
    
    private void Update()
    {
        if (_attackTrack == null || _attackHitDone)
            return;

        TrackEntry current = skeletonAnimation.AnimationState.GetCurrent(0);

        if (current != _attackTrack)
        {
            CancelAttack();
            return;
        }

        float hitTime = _attackTrack.AnimationEnd * attackHitRate;

        if (_attackTrack.TrackTime < hitTime)
            return;

        _attackHitDone = true;

        Action onHit = _onAttackHit;
        _onAttackHit = null;
        onHit?.Invoke();
    }
    
    public void PlayIdle()
    {
        if (_isDead || IsOneShotPlaying())
            return;

        PlayLoop(idleAnimationName);
    }

    public void PlayWalk()
    {
        if (_isDead || IsOneShotPlaying())
            return;

        PlayLoop(walkAnimationName);
    }

    public void PlayAttack(Action onHit)
    {
        if (skeletonAnimation == null)
        {
            onHit?.Invoke();
            return;
        }

        if (_isDead || IsOneShotPlaying())
            return;

        _attackHitDone = false;
        _onAttackHit = onHit;

        _attackTrack = skeletonAnimation.AnimationState.SetAnimation(0, attackAnimationName, false);
        skeletonAnimation.AnimationState.AddAnimation(0, idleAnimationName, true, 0f);
    }
    
    public void PlayHit()
    {
        if (skeletonAnimation == null || _isDead || !useHit)
            return;

        // 공격이나 다른 단발 동작 중에는 피격 모션을 생략
        if (IsOneShotPlaying())
            return;

        skeletonAnimation.AnimationState.SetAnimation(0, hitAnimationName, false);
        skeletonAnimation.AnimationState.AddAnimation(0, idleAnimationName, true, 0f);
    }

    public void PlayDie(Action onComplete)
    {
        if (skeletonAnimation == null)
        {
            onComplete?.Invoke();
            return;
        }
        
        CancelAttack();

        _isDead = true;
        _isActionPlaying = false;

        skeletonAnimation.AnimationState.ClearTracks();

        TrackEntry dieTrack = skeletonAnimation.AnimationState.SetAnimation(0, dieAnimationName, false);

        dieTrack.Complete += HandleComplete;

        void HandleComplete(TrackEntry entry)
        {
            entry.Complete -= HandleComplete;
            onComplete?.Invoke();
        }
    }
    
    private void CancelAttack()
    {
        _attackTrack = null;
        _onAttackHit = null;
        _attackHitDone = false;
    }

    public void ResetState()
    {
        if (skeletonAnimation == null)
            return;
        
        CancelAttack();

        _isDead = false;

        skeletonAnimation.Initialize(false);
        skeletonAnimation.AnimationState.ClearTracks();
        skeletonAnimation.Skeleton.SetToSetupPose();
        skeletonAnimation.AnimationState.SetAnimation(0, idleAnimationName, true);
        skeletonAnimation.Update(0f);
    }

    private void PlayLoop(string animationName)
    {
        if (skeletonAnimation == null)
            return;

        TrackEntry current = skeletonAnimation.AnimationState.GetCurrent(0);

        if (current?.Animation?.Name == animationName)
            return;

        skeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
    }
    
    private bool IsOneShotPlaying()
    {
        if (skeletonAnimation == null)
            return false;

        TrackEntry current =
            skeletonAnimation.AnimationState.GetCurrent(0);

        if (current == null || current.Animation == null)
            return false;

        return !current.Loop && current.TrackTime < current.AnimationEnd;
    }
}
