<script module lang="ts">
  const patternSeed = Math.floor(Math.random() * 4294967296);
</script>

<script lang="ts">
  import { detectPortraitColor, portraitArtworkLayout } from '../portraitColor';
  import { getBossPortraitUrl } from '../bossPortraits';
  import { getWowheadCdnIconUrl } from '../selectionIcons';

  type Props = {
    slug: string;
    iconName?: string | null;
    alt?: string;
    size?: number;
  };

  let { slug, iconName, alt = '', size = 30 }: Props = $props();
  const gradientId = $props.id();
  const src = $derived(
    iconName
      ? iconName.startsWith('https://')
        ? iconName
        : iconName.startsWith('ui-ej-')
          ? `https://wow.zamimg.com/images/wow/journal/${iconName}`
          : getWowheadCdnIconUrl(iconName, 'medium')
      : getBossPortraitUrl(slug)
  );
  let failedSource = $state<string | null>(null);
  let detected = $state<{
    src: string;
    color: string | null;
    fit: string;
    lightX: number;
    lightY: number;
  } | null>(null);
  const accent = $derived(detected?.src === src ? detected.color : null);
  const lightX = $derived(detected?.src === src ? detected.lightX : 50);
  const lightY = $derived(detected?.src === src ? detected.lightY : 50);
  const spirals = $derived.by(() => {
    const reach = Math.hypot(Math.max(lightX, 100 - lightX), Math.max(lightY, 100 - lightY)) + 15;
    return Array.from({ length: 3 }, (_, arm) => {
      const points = Array.from({ length: 65 }, (_, step) => {
        const progress = step / 64;
        const angle = (arm * Math.PI * 2) / 3 + progress * Math.PI * 2.4;
        const radius = 8 + progress * reach + Math.sin(progress * 19 + arm * 2) * 2.5;
        return `${step ? 'L' : 'M'}${50 + Math.cos(angle) * radius},${55 + Math.sin(angle) * radius * (0.88 + arm * 0.05)}`;
      });
      return { path: points.join(' '), width: 3 + arm * 1.3 };
    });
  });
  const rays = $derived.by(() => {
    let seed = patternSeed;
    for (const character of slug) seed = (Math.imul(seed, 31) + character.charCodeAt(0)) >>> 0;
    const random = () => {
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
      return seed / 4294967296;
    };
    return Array.from({ length: 9 }, (_, index) => {
      const angle = ((index + random() * 0.35) / 9) * Math.PI * 2;
      const width = 0.045 + random() ** 1.5 * 0.215;
      const directionX = Math.cos(angle);
      const directionY = Math.sin(angle);
      const edgeX = directionX > 0 ? (100 - lightX) / directionX : -lightX / directionX;
      const edgeY = directionY > 0 ? (100 - lightY) / directionY : -lightY / directionY;
      const length = Math.min(edgeX, edgeY) * (1.15 + random() * 0.6);
      const bend = (random() - 0.5) * 0.08;
      const point = (radians: number, distance = length) =>
        `${50 + Math.cos(radians) * distance},${55 + Math.sin(radians) * distance}`;
      return {
        points: `50,55 ${point(angle - width, length * 0.8)} ${point(angle - width * 0.5, length * 0.95)} ${point(angle + bend)} ${point(angle + width * 0.5, length * 0.9)} ${point(angle + width, length * 0.7)}`,
        opacity: 0.65 + random() * 0.35
      };
    });
  });

  function detectColor(event: Event) {
    const image = event.currentTarget as HTMLImageElement;
    const canvas = document.createElement('canvas');
    canvas.width = image.naturalWidth || 32;
    canvas.height = image.naturalHeight || 32;
    const context = canvas.getContext('2d');
    if (!context) {
      console.warn('Unable to detect boss icon colors: canvas is unavailable', { src });
      return;
    }
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    try {
      const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
      detected = {
        src: image.getAttribute('src')!,
        color: detectPortraitColor(pixels),
        ...portraitArtworkLayout(pixels, canvas.width, canvas.height)
      };
    } catch (error) {
      if (!(error instanceof DOMException) || error.name !== 'SecurityError') throw error;
      console.warn('Unable to detect boss icon colors: image does not allow canvas access', {
        src
      });
    }
  }
</script>

{#if src && failedSource !== src}
  <span
    class="boss-portrait"
    class:journal={src.includes('/journal/')}
    style={`width: ${size}px; height: ${size}px; --boss-accent: ${accent ?? '#7b8995'}; --boss-light-x: ${lightX}%; --boss-light-y: ${lightY}%`}
  >
    <svg class="boss-rays" viewBox="0 0 100 100" aria-hidden="true" focusable="false">
      <defs>
        <filter id={`${gradientId}-outline`} x="-10%" y="-10%" width="120%" height="120%">
          <feComponentTransfer in="SourceGraphic" result="artwork">
            <feFuncA type="linear" slope="5" intercept="-4" />
          </feComponentTransfer>
          <feGaussianBlur in="artwork" stdDeviation="1.1" result="separation" />
          <feFlood
            flood-color="color-mix(in srgb, var(--boss-accent) 25%, #101018)"
            flood-opacity="0.7"
          />
          <feComposite in2="separation" operator="in" result="backing" />
          <feGaussianBlur in="artwork" stdDeviation="0.45" />
          <feComponentTransfer result="outline">
            <feFuncA type="linear" slope="2" />
          </feComponentTransfer>
          <feFlood flood-color="white" />
          <feComposite in2="outline" operator="in" result="white-edge" />
          <feMerge>
            <feMergeNode in="backing" />
            <feMergeNode in="white-edge" />
            <feMergeNode in="artwork" />
          </feMerge>
        </filter>
        <radialGradient id={gradientId} gradientUnits="userSpaceOnUse" cx="50" cy="55" r="72">
          <stop offset="0%" stop-color="white" />
          <stop offset="28%" stop-color="white" />
          <stop offset="65%" stop-color="color-mix(in srgb, var(--boss-accent) 45%, white)" />
          <stop offset="90%" stop-color="color-mix(in srgb, var(--boss-accent) 65%, white)" />
          <stop offset="100%" stop-color="var(--boss-accent)" stop-opacity="0.8" />
        </radialGradient>
        <filter id={`${gradientId}-distort`} x="-20%" y="-20%" width="140%" height="140%">
          <feTurbulence
            type="fractalNoise"
            baseFrequency="0.025"
            numOctaves="2"
            seed="7"
            result="noise"
          />
          <feDisplacementMap
            in="SourceGraphic"
            in2="noise"
            scale="4"
            xChannelSelector="R"
            yChannelSelector="G"
            result="rough"
          />
        </filter>
        <radialGradient id={`${gradientId}-vignette`}>
          <stop offset="55%" stop-color="#080910" stop-opacity="0" />
          <stop offset="100%" stop-color="#080910" stop-opacity="0.3" />
        </radialGradient>
      </defs>
      <g
        transform={`translate(${lightX - 50} ${lightY - 55})`}
        filter={`url(#${gradientId}-distort)`}
      >
        <g class="dark-rings" fill="none">
          {#each spirals as spiral (spiral.path)}
            <path d={spiral.path} stroke-width={spiral.width + 7} />
          {/each}
        </g>
        <g class="light-rings" fill="none">
          {#each spirals as spiral (spiral.path)}
            <path d={spiral.path} stroke-width={spiral.width} />
          {/each}
        </g>
        {#each rays as ray (ray.points)}
          <polygon
            class="ray-fan"
            points={ray.points}
            opacity={ray.opacity}
            fill={`url(#${gradientId})`}
          />
        {/each}
      </g>
      <rect width="100" height="100" fill={`url(#${gradientId}-vignette)`} />
    </svg>
    <img
      class="boss-icon"
      {src}
      {alt}
      style={`${detected?.src === src ? detected.fit : ''}; filter: saturate(1.15) url(#${gradientId}-outline)`}
      crossorigin="anonymous"
      onload={detectColor}
      onerror={() => {
        console.error('Unable to load boss icon', { src });
        failedSource = src ?? null;
      }}
    />
  </span>
{/if}

<style>
  .boss-portrait {
    position: relative;
    display: inline-block;
    flex-shrink: 0;
    overflow: hidden;
    box-sizing: border-box;
    vertical-align: middle;
    border: 1px solid color-mix(in srgb, var(--boss-accent) 45%, white);
    border-radius: 3px;
    background: radial-gradient(
      ellipse at var(--boss-light-x) var(--boss-light-y),
      color-mix(in srgb, var(--boss-accent) 40%, white),
      var(--boss-accent) 40%,
      color-mix(in srgb, var(--boss-accent) 35%, #080910) 78%
    );
    box-shadow: 0 0 0 1px color-mix(in srgb, var(--boss-accent) 35%, transparent);
  }

  .boss-rays {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
    pointer-events: none;
    filter: saturate(1.65);
  }

  .light-rings {
    stroke: color-mix(in srgb, var(--boss-accent) 30%, white);
    opacity: 0.6;
  }

  .dark-rings {
    stroke: color-mix(in srgb, var(--boss-accent) 30%, #080910);
    opacity: 0.8;
  }

  .boss-icon {
    position: absolute;
    left: 0;
    top: 0;
    display: block;
    width: 100%;
    height: 100%;
    object-fit: contain;
    object-position: center bottom;
    transform: translateY(0.5px) scale(1.2);
    transform-origin: center bottom;
  }
</style>
