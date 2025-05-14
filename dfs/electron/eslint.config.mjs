import js from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";
import pluginReact from "eslint-plugin-react";
import { defineConfig } from "eslint-define-config";
import noConsoleLogRule from "./custom-rules/no-console-log.js";

const jsConfigs = js.configs;

export default defineConfig([
  jsConfigs.recommended,
  {
    files: ["**/*.{js,ts,jsx,tsx}"],
    languageOptions: {
      globals: globals.browser,
    },
    plugins: {
      custom: {
        rules: {
          "no-console-log": noConsoleLogRule,
        },
      },
      // You don't strictly need to define the react plugin here again
      // if you're also using pluginReact.configs.flat.recommended later,
      // but it doesn't hurt.
      react: pluginReact,
    },
    rules: {
      "custom/no-console-log": "error",
      // Keep the react/react-in-jsx-scope: "off" here as well,
      // but the override below is the critical part.
      "react/react-in-jsx-scope": "off",
    },
  },
  ...tseslint.configs.recommended,
  pluginReact.configs.flat.recommended, // This config enables the rule
  {
    // This configuration comes LAST and overrides the setting
    files: ["**/*.{jsx,tsx}"], // Apply this override specifically to JSX/TSX files
    rules: {
      "react/react-in-jsx-scope": "off", // Explicitly disable it again here
    },
  },
]);
