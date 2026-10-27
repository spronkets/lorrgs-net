<script lang="ts">
  import { detectPortraitColor, fitPortraitArtwork } from '../portraitColor';
  import { getBossPortraitUrl } from '../bossPortraits';
  import { getWowheadCdnIconUrl } from '../selectionIcons';

  type Props = {
    slug: string;
    iconName?: string | null;
    alt?: string;
    size?: number;
  };

  let { slug, iconName, alt = '', size = 30 }: Props = $props();
  const src = $derived(
    iconName ? getWowheadCdnIconUrl(iconName, 'medium') : getBossPortraitUrl(slug)
  );
  let failedSource = $state<string | null>(null);
  let detected = $state<{ src: string; color: string | null; fit: string } | null>(null);
  const accent = $derived(detected?.src === src ? detected.color : null);

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
        fit: fitPortraitArtwork(pixels, canvas.width, canvas.height)
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
    style={`width: ${size}px; height: ${size}px; --boss-accent: ${accent ?? '#7b8995'}`}
  >
    <img
      class="boss-icon"
      {src}
      {alt}
      style={detected?.src === src ? detected.fit : ''}
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
    border: 1px solid color-mix(in srgb, var(--boss-accent) 70%, white);
    border-radius: 3px;
    background: radial-gradient(
      circle at 50% 30%,
      var(--boss-accent),
      color-mix(in srgb, var(--boss-accent) 30%, #101820)
    );
    box-shadow: 0 0 0 1px color-mix(in srgb, var(--boss-accent) 35%, transparent);
  }

  .boss-icon {
    position: absolute;
    left: 0;
    top: 0;
    display: block;
    width: 100%;
    height: 100%;
    object-fit: contain;
    object-position: center;
  }
</style>
