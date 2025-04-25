module.exports = {
  preset: "ts-jest",
  collectCoverage: true,
  collectCoverageFrom: [
    "src/**/*.ts",
    "src/**/*.tsx",
    "!src/**/*.d.ts",
    "!src/types/**/*",
    "!src/preload.ts",
    "!src/renderer.ts",
    "!src/App.tsx",
  ],
  coverageDirectory: "./coverage",
  coverageReporters: ["lcov", "text", "json"],
  transform: {
    "^.+\\.tsx?$": ["ts-jest", { tsconfig: "tsconfig.json" }],
  },
};
