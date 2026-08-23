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

    private TrackEntry _deathTrack;
    private bool _isActionPlaying;
    private bool _isDead;

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

    public void PlayAttack()
    {
        if (skeletonAnimation == null || _isDead)
            return;

        if (IsOneShotPlaying())
            return;

        skeletonAnimation.AnimationState.SetAnimation(
            0,
            attackAnimationName,
            false);

        skeletonAnimation.AnimationState.AddAnimation(
            0,
            idleAnimationName,
            true,
            0f);
    }
    public void PlayHit()
    {
        if (skeletonAnimation == null)
            return;

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
        if (skeletonAnimation == null)
        {
            onComplete?.Invoke();
            return;
        }

        _isDead = true;
        _isActionPlaying = false;

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
        if (skeletonAnimation == null)
            return;

        _isDead = false;

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

        if (current == null || current.Animation == null)
            return false;

        return !current.Loop &&
               current.TrackTime < current.AnimationEnd;
    }
}
