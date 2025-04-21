import * as fs from "fs";
import { resolve } from "path";
import * as protobuf from "protobufjs";
import { MethodDeclarationStructure, OptionalKind, Project } from "ts-morph";

const descriptorPath = resolve(__dirname, "../src/types/descriptors.json");
const outputDir = resolve(__dirname, "../src/types/wrap");

const descriptor = JSON.parse(fs.readFileSync(descriptorPath, "utf-8"));
const root = protobuf.Root.fromJSON(descriptor);
root.resolveAll();

const targetServiceName = "Ui";

// Set up ts-morph project
const project = new Project();
generateWrapper(project, "NodeServiceClient", (method) => {
  const argString = method.requestType === "Empty" ? "{}" : "req";
  let call = `await window.electronAPI.callGrpc(\"${pascalToCamel(
    method.name,
  )}\", ${argString})`;
  call =
    method.responseType === "Empty"
      ? call + ";"
      : `return ${method.responseType}.fromBinary(${call});`;

  return {
    name: method.name,
    isAsync: true,
    parameters:
      method.requestType === "Empty"
        ? []
        : [{ name: "req", type: method.requestType }],
    returnType: `Promise<${
      method.responseType === "Empty" ? "void" : method.responseType
    }>`,
    statements: [call],
  };
});
generateWrapper(project, "NodeServiceSerializer", (method) => {
  return {
    name: pascalToCamel(method.name),
    isAsync: false,
    isStatic: true,
    parameters: [{ name: "req", type: method.responseType }],
    returnType: `Uint8Array`,
    statements: [`return ${method.responseType}.toBinary(req);`],
  };
});

function generateWrapper(
  project: Project,
  outputName: string,
  getMethodDescription: (
    method: protobuf.Method,
  ) => OptionalKind<MethodDeclarationStructure>,
) {
  const file = project.createSourceFile(
    resolve(outputDir, `${outputName}.ts`),
    "",
    {
      overwrite: true,
    },
  );

  const service = findService(root, targetServiceName);
  if (service) {
    const importsMap = new Map<string, Set<string>>();
    function registerImport(typeName: string, fromModule: string) {
      if (fromModule === "fs") {
        fromModule = `@/types/fs/filesystem`;
      } else if (fromModule === "rpc_common") {
        fromModule = `@/types/rpc_common`;
      } else if (fromModule === "Ui") {
        fromModule = `@/types/rpc/uiservice`;
      } else {
        throw new Error("invalid");
      }

      if (!importsMap.has(fromModule)) {
        importsMap.set(fromModule, new Set());
      }
      importsMap.get(fromModule)!.add(typeName);
    }

    const methods: OptionalKind<MethodDeclarationStructure>[] | undefined =
      Object.values(service.methods).map((method) => {
        const reqPackage = extractPackage(method.resolvedRequestType!.fullName);
        const resPackage = extractPackage(
          method.resolvedResponseType!.fullName,
        );

        method.requestType = extractType(method.requestType);
        method.responseType = extractType(method.responseType);

        registerImport(method.requestType, reqPackage);
        registerImport(method.responseType, resPackage);

        return getMethodDescription(method);
      });

    for (const [modulePath, typeSet] of importsMap.entries()) {
      file.addImportDeclaration({
        namedImports: [...typeSet],
        moduleSpecifier: modulePath,
        leadingTrivia: "// @ts-ignore i hate typescript",
      });
    }

    file.addClass({
      name: `${outputName}`,
      isExported: true,
      methods,
    });

    file.saveSync();
    console.log(`Wrote ${file.getFilePath()}`);
  } else {
    throw new Error(`Service "${targetServiceName}" not found.`);
  }
}

// Helper to find a service in a nested namespace
function findService(
  root: protobuf.Root,
  serviceName: string,
): protobuf.Service | undefined {
  for (const ns of Object.values(root.nested ?? {})) {
    if (ns instanceof protobuf.Namespace) {
      const maybeService = ns.nested?.[serviceName];
      if (maybeService instanceof protobuf.Service) {
        return maybeService;
      }
    }
  }
  return undefined;
}

function extractType(fullyQualified: string) {
  fullyQualified = fullyQualified.replace(/^\./, "");
  const parts = fullyQualified.split(".");
  const type = parts.pop()!;
  const pkg = parts.join(".");

  if (type === "String" && pkg === "") {
    return "String$";
  }
  return type;
}

function extractPackage(fullyQualified: string) {
  fullyQualified = fullyQualified.replace(/^\./, "");
  const parts = fullyQualified.split(".");
  const type = parts.pop()!;
  const pkg = parts.join(".");
  return pkg;
}

function pascalToCamel(str: string): string {
  return str.charAt(0).toLowerCase() + str.slice(1);
}
