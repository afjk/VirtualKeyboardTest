namespace MozcShim
{
    /// <summary>
    /// What <see cref="JapaneseKeyboardBuffer"/> needs from a conversion engine.
    /// <see cref="MozcSession"/> implements it; tests can supply a fake.
    /// </summary>
    public interface IKanaKanjiConverter
    {
        /// <summary>Printable ASCII key; romaji is converted to kana by the engine.</summary>
        MozcState SendKey(char key);
        /// <summary>Mozc special key name: SPACE, ENTER, BACKSPACE, DEL, LEFT, RIGHT, UP, DOWN, ESCAPE.</summary>
        MozcState SendSpecialKey(string specialKey);
        MozcState SelectCandidate(int candidateId);
        /// <summary>Commit the current preedit or conversion as it is.</summary>
        MozcState Submit();
    }
}
