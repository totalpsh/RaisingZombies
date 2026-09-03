using System;
using Spine;
using Spine.Unity;
using UnityEngine;

public class UnitAnimation : MonoBehaviour
{
    [SerializeField]
    private SkeletonAnimation skeletonAnimation;

    [Header("Animation Names")]
    [SerializeField]
    private string idleAnimationName = "Idle";

    [SerializeField]
    private string walkAnimationName = "Walk";

    [SerializeField]
    private string attackAnimationName = "Attack2";

    [SerializeField]
    private string hitAnimationName = "Hit";

    [SerializeField]
    private string dieAnimationName = "Die";

    [SerializeField]
    private bool useHit = true;

    [SerializeField, Range(0f, 0.99f)]
    private float attackHitRate = 0.55f;

    private TrackEntry _attackTrack;
    private Action _onAttackHit;
    private bool _isDead;

    public bool IsBusy =>
        _isDead ||
        IsOneShotPlaying();

    private void Update()
    {
        if (_attackTrack == null)
            return;

        TrackEntry current =
            skeletonAnimation.AnimationState.GetCurrent(0);

        if (current != _attackTrack)
        {
            CancelAttack();
            return;
        }

        float hitTime =
            _attackTrack.AnimationEnd *
            attackHitRate;

        if (_attackTrack.TrackTime < hitTime)
            return;

        Action onHit = _onAttackHit;

        CancelAttack();
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

    public bool PlayAttack(Action onHit)
    {
        if (_isDead || IsOneShotPlaying())
            return false;

        if (skeletonAnimation == null)
        {
            onHit?.Invoke();
            return true;
        }

        _onAttackHit = onHit;

        _attackTrack =
            skeletonAnimation.AnimationState.SetAnimation(
                0,
                attackAnimationName,
                false);

        skeletonAnimation.AnimationState.AddAnimation(
            0,
            idleAnimationName,
            true,
            0f);

        return true;
    }

    public void PlayHit()
    {
        if (skeletonAnimation == null ||
            _isDead ||
            !useHit ||
            IsOneShotPlaying())
        {
            return;
        }

        skeletonAnimation.AnimationState.SetAnimation(
            0,
            hitAnimationName,
            false);

        skeletonAnimation.AnimationState.AddAnimation(
            0,
            idleAnimationName,
            true,
            0f);
    }

    public void PlayDie(Action onComplete)
    {
        if (_isDead)
            return;

        CancelAttack();
        _isDead = true;

        if (skeletonAnimation == null)
        {
            onComplete?.Invoke();
            return;
        }

        skeletonAnimation.AnimationState.ClearTracks();

        TrackEntry dieTrack =
            skeletonAnimation.AnimationState.SetAnimation(
                0,
                dieAnimationName,
                false);

        dieTrack.Complete += HandleComplete;

        void HandleComplete(TrackEntry entry)
        {
            entry.Complete -= HandleComplete;
            onComplete?.Invoke();
        }
    }

    public void ResetState()
    {
        CancelAttack();
        _isDead = false;

        if (skeletonAnimation == null)
            return;

        skeletonAnimation.Initialize(false);
        skeletonAnimation.AnimationState.ClearTracks();
        skeletonAnimation.Skeleton.SetToSetupPose();

        skeletonAnimation.AnimationState.SetAnimation(
            0,
            idleAnimationName,
            true);

        skeletonAnimation.Update(0f);
    }

    private void PlayLoop(string animationName)
    {
        if (skeletonAnimation == null)
            return;

        TrackEntry current =
            skeletonAnimation.AnimationState.GetCurrent(0);

        if (current?.Animation?.Name == animationName)
            return;

        skeletonAnimation.AnimationState.SetAnimation(
            0,
            animationName,
            true);
    }

    private bool IsOneShotPlaying()
    {
        if (skeletonAnimation == null)
            return false;

        TrackEntry current =
            skeletonAnimation.AnimationState.GetCurrent(0);

        if (current?.Animation == null)
            return false;

        return !current.Loop &&
               current.TrackTime < current.AnimationEnd;
    }

    private void CancelAttack()
    {
        _attackTrack = null;
        _onAttackHit = null;
    }

    private void OnDisable()
    {
        CancelAttack();
    }
}
