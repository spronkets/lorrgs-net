import { mount } from 'svelte';
import '@fontsource-variable/inter/index.css';
import './app.css';
import './lib/styles/_buttons.scss';
import './lib/styles/_inputs.scss';
import App from './App.svelte';

const app = mount(App, {
  target: document.getElementById('app') as HTMLElement
});

export default app;
