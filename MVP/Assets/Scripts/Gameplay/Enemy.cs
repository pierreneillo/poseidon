using Unity.VisualScripting;
using UnityEngine;

public class Enemy : MonoBehaviour
{
  // Public attributes
  [Header("Stats")]
  [SerializeField] protected float maxHp = 15f;
  protected float hp;

  [Header("Rendering")]
  [SerializeField] protected SpriteRenderer hpBar;
  [SerializeField] protected SpriteRenderer fire;
  protected Vector2 hpBarSize;
  protected Vector2 fireSize;
  [SerializeField] protected float min_fire_scale = 0f;

  [SerializeField] protected AudioClip[] hitSounds;



  // protected attributes
  protected Rigidbody2D _rb;
  protected bool _burning;

  public int GPUObstacleID { get; private set; } = -1;

  protected virtual void Start()
  {
    // Movement
    GPUObstacleID = FluidBridge.RegisterObstacle(this);
    _rb = GetComponent<Rigidbody2D>();

    // HP
    hp = maxHp;
    hpBarSize = hpBar.transform.localScale;
    fireSize = fire.transform.localScale;
    _burning = true;

  }


  public bool InflictDamage(float damages)
  {
    if (_burning)
    {

      // HP management
      hp -= damages;


      if (hp <= 0)
      {
        Debug.Log("Enemy dead");
        FluidBridge.UnregisterObstacle(GPUObstacleID);
        Destroy(hpBar);
        Destroy(fire);

        _burning = false;
        SoundManager.instance.KillSound();

        return true;
      }


      // HP bar and Fire
      hpBar.transform.localScale = Vector2.Lerp(new Vector2(0f, hpBarSize.y), hpBarSize, hp / maxHp);
      hpBar.color = Color.Lerp(Color.red, Color.green, hp / maxHp);
      fire.transform.localScale = Vector2.Lerp(new Vector2(min_fire_scale, min_fire_scale), fireSize, hp / maxHp);



      return false;
    }

    return false;
  }

  protected virtual void OnDestroy()
  {
    if (SoundManager.instance != null)
      SoundManager.instance.KillSound();
  }
}
