<script lang="ts">
  export let title: string;
  export let description: string;
</script>

<header class="ranking-page-header">
  <h2>{title}</h2>
  <p>{description}</p>
</header>

<style>
  .ranking-page-header {
    margin-bottom: 24px;
  }
  h2 {
    margin: 0 0 8px;
    font-size: 1.5rem;
    line-height: 1.3;
  }
  p {
    margin: 0;
    line-height: 1.5;
  }
</style>
