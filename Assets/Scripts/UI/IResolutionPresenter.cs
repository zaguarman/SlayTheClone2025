using System.Collections;

/// <summary>
/// Shows the player what each resolved action did. The turn waits on it between actions,
/// so effects play one after another instead of all landing at once.
/// </summary>
public interface IResolutionPresenter {
    IEnumerator WaitUntilIdle();
}
