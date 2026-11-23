using UnityEngine;

internal sealed class ChemistryAudio
{
    const string ResourcePath = "Practicals/ChemistryAudio/";
    readonly AudioSource actions;
    readonly AudioSource intro, water, flame, grind, stir, bubbles;
    readonly AudioClip click, pickup, place, ignite, record;

    internal ChemistryAudio(Transform parent)
    {
        var root = new GameObject("Chemistry audio").transform;
        root.SetParent(parent, false);
        actions = Source(root, "Actions", .55f, false);
        intro = Source(root, "Intro", .34f);
        water = Source(root, "Water", .48f);
        flame = Source(root, "Flame", .30f);
        grind = Source(root, "Grind", .42f);
        stir = Source(root, "Stir", .39f);
        bubbles = Source(root, "Bubbles", .30f);
        click = Load("Click"); pickup = Load("Pickup"); place = Load("Place");
        ignite = Load("Ignite"); record = Load("Record");
    }

    internal void Click() => Play(click);
    internal void PickUp() => Play(pickup);
    internal void Place() => Play(place);
    internal void Ignite() => Play(ignite);
    internal void Record() => Play(record);

    internal void Refresh(bool showIntro, bool flowing, bool flameOn, bool grinding, bool stirring, bool bubbling)
    {
        SetLoop(intro, showIntro);
        SetLoop(water, flowing);
        SetLoop(flame, flameOn);
        SetLoop(grind, grinding);
        SetLoop(stir, stirring);
        SetLoop(bubbles, bubbling);
    }

    internal void Stop()
    {
        actions.Stop();
        Refresh(false, false, false, false, false, false);
    }

    void Play(AudioClip clip)
    {
        if (clip != null) actions.PlayOneShot(clip);
    }

    static AudioClip Load(string name) => Resources.Load<AudioClip>(ResourcePath + name);

    static AudioSource Source(Transform parent, string name, float volume, bool loop = true)
    {
        var source = new GameObject(name).AddComponent<AudioSource>();
        source.transform.SetParent(parent, false);
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0;
        source.volume = volume;
        if (loop) source.clip = Load(name);
        return source;
    }

    static void SetLoop(AudioSource source, bool playing)
    {
        if (playing && !source.isPlaying && source.clip != null) source.Play();
        else if (!playing && source.isPlaying) source.Stop();
    }
}
