//import '@testing-library/jest-dom';
//console.log('hello, world!');
module.exports = {
  testPathIgnorePatterns: ['<rootDir>/node_modules', '<rootDir>/dist'], // might want?
 /* moduleNameMapper: {
    '@components(.*)': '<rootDir>/src/components$1' // might want?
  },*/
  moduleDirectories: ['<rootDir>/node_modules', '<rootDir>/src'],
  setupFilesAfterEnv: ['<rootDir>/src/setupTests.ts'], // this is the KEY
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
  moduleNameMapper: {
    "^@/(.*)$": "<rootDir>/src/$1",
  },
  passWithNoTests: true,
  coverageReporters: ["lcov", "text", "json"],
  transform: {
    "^.+\\.tsx?$": ["ts-jest", { tsconfig: "tsconfig.json" }],
  },
  testEnvironment: 'jsdom',
};
