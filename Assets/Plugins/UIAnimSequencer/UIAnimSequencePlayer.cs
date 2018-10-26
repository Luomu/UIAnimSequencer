using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIAnimSequencePlayer : MonoBehaviour
{
#if UNITY_EDITOR
    private Object m_previouslySelectedObject = null;
#endif
    public List<UIAnimSequence> AnimSequences { get { return m_animSequences; } }

    public UIAnimSequence DefaultAnimSequence { get { return m_defaultAnimSequence; } }

    [SerializeField]
    private UIAnimSequence m_defaultAnimSequence;

    [SerializeField]
    private List<UIAnimSequence> m_animSequences;

    public System.Action onAnimationCompleted;

    public bool PlayOnAwake = true;
    public bool SetToInitialStateOnAwake = true;
    public bool ResetAfterPlay = true;

    private UIAnimSequence m_selectedAnimSequence = null;

    public UIAnimSequence CurrentAnimSequence
    {
        get
        {
            return m_selectedAnimSequence ?? m_defaultAnimSequence;
        }
        set
        {
            m_selectedAnimSequence = value;
        }
    }

    public bool Animating { get; set; }
    public bool HasDefaultState { get; set; }

    private void Awake()
    {
        if (Application.isPlaying && CurrentAnimSequence != null)
        {
            if (PlayOnAwake)
                CurrentAnimSequence.RunSequence(this);
            else if (SetToInitialStateOnAwake)
                CurrentAnimSequence.RunSequence(this, true);
        }
    }

    #region Helper methods

    public string GetChildPath(Transform tf)
    {
        string path = string.Empty;
        if (tf.parent != null && transform != tf)
        {
            string parentPath = GetChildPath(tf.parent);
            if (string.IsNullOrEmpty(parentPath))
            {
                path += tf.name;
            }
            else
            {
                path += parentPath + "/" + tf.name;
            }
        }
        return path;
    }

    public Transform FindTargetFromPath(string path)
    {
        return (this == null || transform == null) ? null : (transform.name == path ? transform : transform.Find(path));
    }

    #endregion Helper methods

    #region Player methdods

    public void Play()
    {
        CurrentAnimSequence.RunSequence(this);
    }

    public void Play(string animationName, bool resetAfterPlay = true)
    {
        ResetAfterPlay = resetAfterPlay;
        UIAnimSequence animSequence = null;
        if (m_animSequences != null)
        {
            animSequence = m_animSequences.Find(x =>
            {
                if (x != null)
                {
                    return x.name.Equals(animationName);
                }
                return false;
            });
        }
        if (animSequence == null && m_defaultAnimSequence != null && m_defaultAnimSequence.name.Equals(animationName))
        {
            animSequence = m_defaultAnimSequence;
        }
        if (animSequence != null)
        {
            CurrentAnimSequence = animSequence;
            Play();
        }
    }

    #endregion Player methdods

    #region Editor preview

    public void EditorPreview()
    {
        if (CurrentAnimSequence == null) return;
        LeanTween.init();
        CurrentAnimSequence.RunSequence(this);

#if UNITY_EDITOR
        m_previouslySelectedObject = UnityEditor.Selection.activeObject;
        UnityEditor.Selection.activeObject = this;
#endif
    }

    public void EditorForceUpdate()
    {
        if (Animating)
            LeanTween.update();
    }

    public void ResetAfterPreview()
    {
#if UNITY_EDITOR
        LeanTween.reset();
        CurrentAnimSequence.ResetToPreActionState(this);
        if (m_previouslySelectedObject != null)
        {
            UnityEditor.Selection.activeObject = m_previouslySelectedObject;
        }
#endif
    }

    public void OnCompleteTheWholeThing()
    {
        if (!Application.isPlaying)
        {
            ResetAfterPreview();
        }
        else
        {
            CurrentAnimSequence.ResetToPreActionState(this, ResetAfterPlay);
        }

        if (onAnimationCompleted != null) onAnimationCompleted();
    }

    #endregion Editor preview

    public void SaveActionOriginalState(UIAnimSequence.SequenceStep step, UIAnimSequence.SequenceAction action, RectTransform xform)
    {
        Transform target = FindTargetFromPath(step.TargetPath);
        switch (action.Type)
        {
            case UIAnimSequence.ActionType.Move:
            case UIAnimSequence.ActionType.Fly:
            case UIAnimSequence.ActionType.Shake:
                action.OrigVector = xform.anchoredPosition3D;
                break;

            case UIAnimSequence.ActionType.RotateZ:
                action.OrigVector = xform.rotation.eulerAngles;
                break;

            case UIAnimSequence.ActionType.Scale:
                action.OrigVector = xform.localScale;
                break;

            case UIAnimSequence.ActionType.Fade:
                {
                    var cg = target.GetComponent<CanvasGroup>();
                    var graphic = target.GetComponent<Graphic>();
                    if (cg != null)
                    {
                        action.OrigFloat = cg.alpha;
                    }
                    else if (graphic != null)
                    {
                        action.OrigFloat = graphic.color.a;
                    }
                    break;
                }
            case UIAnimSequence.ActionType.Hide:
            case UIAnimSequence.ActionType.Show:
                action.OrigFloat = target.gameObject.activeSelf ? 1 : 0;
                break;

            default:
                break;
        }
    }

    public void ResetActionToInitialState(UIAnimSequence.SequenceStep step, UIAnimSequence.SequenceAction action)
    {
        var xform = FindTargetFromPath(step.TargetPath);
        if (xform == null)
        {
            // If the game object has been destroyed.
            return;
        }
        
        var rectxform = xform.GetComponent<RectTransform>();
        switch (action.Type)
        {
            case UIAnimSequence.ActionType.Move:
            case UIAnimSequence.ActionType.Fly:
            case UIAnimSequence.ActionType.Shake:
                rectxform.anchoredPosition3D = action.OrigVector;
                break;

            case UIAnimSequence.ActionType.RotateZ:
                xform.rotation = Quaternion.Euler(action.OrigVector);
                break;

            case UIAnimSequence.ActionType.Scale:
                xform.localScale = action.OrigVector;
                break;

            case UIAnimSequence.ActionType.Fade:
                var cg = xform.GetComponent<CanvasGroup>();
                var graphic = xform.GetComponent<Graphic>();
                if (cg != null)
                {
                    cg.alpha = action.OrigFloat;
                }
                else if (graphic != null)
                {
                    graphic.color = graphic.color.WithA(action.OrigFloat);
                }
                break;

            case UIAnimSequence.ActionType.Show:
            case UIAnimSequence.ActionType.Hide:
                xform.gameObject.SetActive(action.OrigFloat > 0f);
                break;
        }
    }
}