# Hollow Tide

Demo jogável de um *survivors-like* (bullet heaven) em **Unity 6.3 LTS**, C#, 2D com URP. Você anda, as armas atacam sozinhas, os inimigos vêm em ondas cada vez maiores e, aos 30 minutos, o Ceifador chega para encerrar a noite.

**Propriedade intelectual:** reproduz *mecânicas*, não conteúdo. Nomes, personagens, armas, inimigos, textos e arte são originais; todos os sprites são gerados por código e todos os sons são sintetizados por fórmulas. Nenhum asset de terceiros.

---

## 1. Como abrir

1. Unity Hub > **Add > Add project from disk** > pasta `SurvivorsDemo`.
2. Editor: **Unity 6.3 LTS (6000.3.x)**. Se a versão exata do `ProjectSettings/ProjectVersion.txt` não estiver instalada, o Hub deixa escolher outra 6000.3.x.
3. Na primeira abertura o script `ProjectBootstrap` ativa o backend do **novo Input System** e pede para reiniciar o editor. Aceite.
4. Menu **Tools > Build Demo**. Ele gera sprites, URP 2D, todo o conteúdo (ScriptableObjects) e as cenas `MainMenu` e `Game`, e abre o menu.
5. Aperte **Play**. O menu principal aparece; "Jogar" leva à seleção de personagem e depois à partida.

Para testar só a partida (sem passar pelo menu), abra `Assets/_Project/Scenes/Game.unity` e aperte Play: ela usa o primeiro personagem desbloqueado.

## 2. Controles

| Ação | Teclado | Gamepad |
|---|---|---|
| Mover (8 direções) | WASD / setas | Analógico esquerdo / D-pad |
| Pausar | Esc / P | Start |
| Escolher carta (level up) | mouse, teclas `1`–`4` | navegar + confirmar |
| Rerrolar / Pular (level up) | `E` / `X` | botões da tela |
| Confirmar (baú, menus) | Enter / Espaço / clique | confirmar |

Não existe botão de ataque.

### Atalhos de desenvolvimento

Overlay no canto da tela (`F3` oculta). Servem para testar sem esperar 30 minutos:

| Tecla | Efeito |
|---|---|
| `F` / `K` | +100 / +1000 inimigos (estresse) |
| `V` | invoca um elite (Hollow Knight): mate-o para ver o baú |
| `G` | sobe de nível agora |
| `L` | sobe todas as armas de nível |
| `N` | dá uma arma nova |
| `O` | dá um passivo (prioriza o que uma evolução exige) |
| `C` | "abre um baú": evolui as armas elegíveis |
| `T` | +5 minutos no relógio |
| `Y` | pula para 29:50 (o Ceifador chega em 10 s) |
| `H` | invencível liga/desliga |
| `B` | **benchmark** de performance (ver seção 6) |
| `R` | reinicia a cena |

Menu **Tools > Save**: apagar o save, revelar o arquivo, dar 5000 de ouro para testar a loja.

## 3. O que tem no jogo

- **Armas (5 comportamentos, níveis 1–8):** Crescent Blade (golpe em arco), Seeker Wisp (teleguiado), Halo Shards (orbitais), Withering Aura (dano contínuo), Meteor Flask (projétil em arco que explode). Cada nível melhora dano, área, quantidade, recarga, duração etc.
- **Passivos (12):** dano, armadura, vida máxima, regeneração, recarga, área, velocidade de projétil, duração, velocidade de movimento, raio de coleta, XP extra, sorte. Limite de 6 armas e 6 passivos.
- **Evolução:** arma no nível 8 + passivo certo, ao abrir um baú. Crescent Blade + Wrath Sigil → Eclipse Edge; Seeker Wisp + Quickening Gear → Wisp Storm; Halo Shards + Lingering Ember → Halo Tempest.
- **Inimigos (6 tipos):** Crawler, Shambler, Swarmer, Brute, Hollow Knight (elite, derruba baú) e o Ceifador.
- **Ondas:** 30 faixas, uma por minuto, sempre crescendo (ritmo, teto de inimigos e vida). Elites a cada 3 minutos, cercos a cada 4. Aos **30:00** surge o Ceifador (rápido, quase invencível); ao te alcançar a partida termina com a tela de vitória (você sobreviveu à noite). Morrer antes é derrota.
- **XP e level up:** gemas atraídas dentro do raio de coleta; 3 opções (4 com sorte); 2 rerolls e 2 skips por partida (a loja aumenta).
- **Drops:** ouro, e raros: cura, ímã (puxa todas as gemas), bomba (limpa a tela, poupa chefes). Elites soltam baú com animação de abertura.
- **Meta-progressão:** ouro persiste no save; loja com 12 melhorias permanentes; 3 personagens (2 se desbloqueiam com ouro), cada um com arma inicial e bônus.
- **Feedback:** números de dano, flash ao dar e levar dano, partículas, screenshake, sons sintetizados (som ligável/desligável e volume nas opções).

## 4. Arquitetura

```
Assets/_Project/
  Scripts/Core/      C# puro, SEM UnityEngine (assembly com noEngineReferences)
  Scripts/Runtime/   MonoBehaviours, ScriptableObjects, UI, áudio, save
  Scripts/Editor/    Tools > Build Demo e geradores (sprites, conteúdo, cenas)
  Tests/EditMode/    Unity Test Framework (NUnit)
  Content/           ScriptableObjects gerados (Enemies, Weapons, Passives, Waves, Characters, Meta)
  Art/Generated/     PNGs gerados por código (não edite à mão)
  Scenes/  Settings/
Tools~/CoreTests/    dotnet test do Core, fora da Unity (a pasta com ~ é ignorada pela Unity)
```

### Simulação separada da parte visual

O `Core` contém toda a lógica que importa e não conhece a Unity: `EnemyWorld`, `GemWorld` e `ProjectileWorld` (struct-of-arrays), `SpatialHashGrid`, `WaveScheduler`, `XpTracker`, sorteio de level up, progressão e cooldown de armas, saúde do jogador, regras de evolução e de drops, loja e save. O `Runtime` só *espelha* esse estado em `SpriteRenderer`s (as `*Views`) e traduz ScriptableObjects em dados do Core.

```
GameSession.Update()   ← único Update do gameplay, ordem explícita
  ├─ pausa / level up / baú / fim de partida (estados que congelam o jogo)
  ├─ EnemySpawner.Tick     WaveScheduler decide, o spawner posiciona no anel fora da tela
  ├─ EnemyWorld.Step       persegue o jogador, knockback, realoca quem ficou para trás
  ├─ RebuildGrid + Separate spatial hash reconstruído por frame; separação entre inimigos
  ├─ ResolveContact        dano por contato com invulnerabilidade curta
  ├─ PlayerWeapons.Tick    cada arma tem um WeaponBehavior
  ├─ ProjectileSystem.Tick ProjectileWorld.Step → eventos de acerto/explosão → HitEnemy
  └─ CollectGems + PickupSystem.Tick
LateUpdate: EnemyViews / GemViews / ProjectileSystem sincronizam os sprites
```

### Decisões principais

- **Sem `Collider2D` nos inimigos.** Colisão, busca do mais próximo e separação usam um spatial hash próprio (`SpatialHashGrid`, listas encadeadas em arrays, sem alocação).
- **Object pooling** com `UnityEngine.Pool.ObjectPool` para inimigos, gemas, moedas, projéteis, explosões, efeitos e pickups; números de dano e partículas usam pools de tamanho fixo.
- **Sem alocação no estado estável:** nada de LINQ, `GetComponent` ou `new` no código quente; buffers reutilizados; textos de UI só mudam quando o valor muda (com cache de strings de números).
- **Conteúdo em ScriptableObjects** editáveis no Inspector. `Tools > Build Demo` cria o que falta e **nunca sobrescreve** o que você editou; `Tools > Reset Content` volta ao padrão.
- **`ContentCatalog`** lista tudo e é reconstruído a cada Build Demo varrendo a pasta `Content`.
- **UI em uGUI construída por código** (sem prefabs nem TextMeshPro Essentials para importar).
- **Save** em JSON (`Application.persistentDataPath/save.json`), gravado de forma atômica; arquivo corrompido vira save novo (o original é guardado como `.corrupt`). Melhorias e personagens são gravados por *id*, não por posição.

## 5. Testes

- **Na Unity:** Window > General > Test Runner > **EditMode** > Run All.
- **Fora da Unity** (só o Core): `dotnet test Tools~/CoreTests` (compila os mesmos arquivos do projeto).

Cobertura pedida: curva de XP (`XpTests`), seleção de opções do level up (`LevelUpTests`), cooldown e dano das armas (`WeaponTests`, `ProjectileTests`) e spawn por onda (`WaveTests`). Também: spatial hash, mundo de inimigos, saúde, drops, baú, loja, save e regras de evolução.

## 6. Performance

Meta: 60 FPS com 500+ inimigos e 200+ projéteis/gemas.

- `SimulationPerformanceTests` roda a simulação com **1000 inimigos, 300 projéteis e 400 gemas**: exige **zero bytes alocados** em 600 frames (o teste foi validado injetando uma alocação de propósito) e custo bem abaixo do orçamento de 16,6 ms.
- Batching: cada tipo de inimigo tem seu `sortingOrder`, e o fundo usa uma ordem por variante de tile, para o renderer 2D agrupar por textura em vez de trocar de textura a cada sprite.
- **Benchmark dentro do jogo:** na partida, aperte `B`. Por 20 s ele mantém 600 inimigos, dá 6 armas no nível máximo e fica invencível; no fim mostra **FPS médio, 1% pior e a quantidade de coletas de GC**. Ande com WASD durante o teste. Rode no Editor e, para números reais, num build (File > Build Profiles).

## 7. Como adicionar conteúdo

Regra geral: crie o asset no menu **Assets > Create > Hollow Tide > ...**, edite no Inspector e rode **Tools > Build Demo** para o `ContentCatalog` reencontrá-lo.

### Adicionar uma ARMA

1. **Assets > Create > Hollow Tide > Weapon** (na pasta `Content/Weapons`).
2. Preencha:
   - `weaponId`, `displayName`, `description`, `icon`.
   - `behavior`: um de `ArcSlash`, `HomingBolt`, `Orbit`, `Aura`, `LobbedProjectile`.
   - `baseStats` (nível 1): `Damage`, `Area` (multiplicador), `Cooldown` (s), `Duration` (multiplicador), `ProjectileSpeed` (multiplicador), `Knockback`, `Amount`, `Pierce`.
   - `levelUps`: 7 melhorias. O índice 0 vale ao chegar no nível 2 e o 6 no nível 8. Cada uma soma `DamageAdd`, `AmountAdd`, `PierceAdd`, `KnockbackAdd` (valores) e `AreaPct`, `CooldownPct` (negativo reduz), `DurationPct`, `SpeedPct` (frações: 0.2 = +20%).
   - `baseSize`: raio do arco/aura/explosão, raio do projétil ou distância da órbita. `baseSpeed`: unidades/s (projéteis) ou graus/s (orbitais). `baseDuration`: segundos de vida do projétil ou tempo ativo dos orbitais.
   - `effectSprite` e `effectTint`.
3. **Evolução (opcional):** crie uma segunda arma com `evolutionOnly` marcado (ela não aparece no level up) e, na arma base, preencha `evolvesInto` e `requiredPassive`.
4. Para ser oferecida no level up ela só precisa estar no catálogo (Build Demo). Para ser a **arma inicial** de um personagem, arraste-a em `startingWeapon` do `CharacterData`.
5. **Comportamento novo?** Crie `MinhaArmaBehavior : WeaponBehavior` (implemente `Tick`; use `Session`, `Weapon.Resolved(Session.Stats)` e `Session.HitEnemy/DamageArea`), adicione um valor em `WeaponBehaviorType` e uma linha em `WeaponBehaviorFactory.Create`. Nada mais no jogo precisa mudar.

### Adicionar um INIMIGO

1. **Create > Hollow Tide > Enemy** (`Content/Enemies`).
2. Preencha `stats` (`MaxHp`, `Speed`, `ContactDamage`, `Radius` de colisão em unidades, `KnockbackResistance` de 0 a 1), `xpValue` (gema derrubada), `sprite` (+ `flashSprite`, silhueta branca do flash), `visualScale`, `deathColor`.
3. **`sortingOrder`: use um valor único entre 10 e 19** (ainda livre: 16–19). É o que mantém o batching bom com centenas de inimigos.
4. Elite/chefe: marque `dropsChest` (solta baú) e/ou `isBoss` (bomba só faz dano simbólico; não solta drops raros).
5. Ele só aparece no jogo quando uma **onda** o usa (`enemies` ou `bursts`).

### Adicionar uma ONDA

Uma `WaveData` é uma faixa da tabela: vale do `startMinute` até o início da próxima onda (o jogo escolhe a de maior `startMinute` que já começou).

1. **Create > Hollow Tide > Wave** (`Content/Waves`).
2. Preencha:
   - `startMinute`: minuto da partida em que começa.
   - `spawnsPerSecond`: inimigos por segundo (spawn contínuo).
   - `maxAlive`: teto de inimigos vivos para o spawn contínuo (rajadas ignoram o teto).
   - `hpMultiplier`: multiplica a vida de quem nasce nesta faixa.
   - `enemies`: lista de `enemy` + `weight` (peso no sorteio).
   - `bursts`: rajadas únicas: `enemy`, `count`, `atSecond` (após o início da faixa) e `surround` (cerco: distribui igualmente ao redor do jogador).
3. **A tabela deve sempre crescer:** `spawnsPerSecond` e `maxAlive` não podem diminuir de uma faixa para a seguinte. O Build Demo e o início da partida avisam no Console se isto for violado.
4. Rode Build Demo. Para ver o resultado rápido: `T` (+5 min) ou `Y` (29:50) no jogo.

### Outros

- **Passivo:** Create > Hollow Tide > Passive (`stat`, `amountPerLevel`, `maxLevel`).
- **Personagem:** Create > Hollow Tide > Character (`startingWeapon`, `bonuses`, `unlockCost`).
- **Melhoria permanente:** Create > Hollow Tide > Meta Upgrade (`stat`, `amountPerLevel`, `maxLevel`, `baseCost`, `costGrowth`).

## 8. Limitações conhecidas

- O código de Runtime e de Editor foi escrito sem acesso a uma instalação da Unity. A lógica do Core é totalmente testada (`dotnet test`), e o resto foi checado contra stubs da API, mas o primeiro Play no editor é o primeiro teste de verdade. Se algo do URP 2D ou de pacotes divergir na sua versão, o `UrpSetup` e o Console dizem o que ajustar.
- Balanceamento (dano, vida, taxas de drop, curva de XP) é um primeiro passe e pede tuning jogando.
- Sem música (só efeitos), sem localização (textos em português) e sem tela de opções de vídeo.
